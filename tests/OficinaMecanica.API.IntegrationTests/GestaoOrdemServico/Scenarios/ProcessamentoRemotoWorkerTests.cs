using System.Net;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using OficinaMecanica.API.IntegrationTests.Fixtures;
using OficinaMecanica.API.IntegrationTests.GestaoOrdemServico.Factories;
using OficinaMecanica.Application;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Infrastructure;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Infrastructure.Hosting;

namespace OficinaMecanica.API.IntegrationTests.GestaoOrdemServico.Scenarios;

[Collection(OficinaMecanicaApiCollection.Nome)]
public sealed class ProcessamentoRemotoWorkerTests : IAsyncLifetime
{
    private const string FilaEntrada = "https://sqs.us-east-1.amazonaws.com/123456789012/os.fifo";
    private const string FilaExecucao = "https://sqs.us-east-1.amazonaws.com/123456789012/execucao.fifo";
    private const string FilaOrcamento = "https://sqs.us-east-1.amazonaws.com/123456789012/orcamento.fifo";
    private readonly OficinaMecanicaApiFixture _fixture;

    public ProcessamentoRemotoWorkerTests(OficinaMecanicaApiFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetarBancoAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private ServiceProvider CriarServicos(IAmazonSQS sqs)
    {
        using var dbContext = _fixture.CriarDbContext();
        return new ServiceCollection().AddLogging().AddApplication()
            .AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = dbContext.Database.GetConnectionString()
            }).Build())
            .AddProcessamentoRemoto(new(FilaEntrada, FilaExecucao, FilaOrcamento), sqs).BuildServiceProvider();
    }

    private async Task<MensagemIntegracao> PrepararFinalizacaoAsync()
    {
        await using var dbContext = _fixture.CriarDbContext();
        await SagaIntegracaoTestDataFactory.CriarAsync(dbContext);
        var saga = await dbContext.Set<SagaOrdemServico>().SingleAsync();
        saga.RegistrarOrcamento(Guid.NewGuid(), 3, 150m);
        saga.RegistrarDecisao(saga.OrcamentoId!.Value, 3, DecisaoOrcamento.Aprovado);
        saga.RegistrarInicioFisico(saga.PreparacaoId!.Value, 3);
        var autorizacao = new MensagemIntegracao(Guid.NewGuid(), "AutorizarExecucao", 1, saga.Id, saga.OrdemServicoId,
            "correlacao-worker", Guid.NewGuid(), DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(new AutorizarExecucaoDados(saga.PreparacaoId.Value, 3), JsonContrato.Options));
        var registro = RegistroOutboxSaga.Criar(autorizacao, 3, "Execucao");
        dbContext.Add(registro);
        dbContext.Entry(registro).Property(nameof(RegistroOutboxSaga.PublicadoEm)).CurrentValue = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();
        return autorizacao with
        {
            MensagemId = Guid.NewGuid(),
            Tipo = "ExecucaoFinalizada",
            CausationId = Guid.NewGuid(),
            Dados = JsonSerializer.SerializeToElement(new ExecucaoFinalizadaDados(saga.PreparacaoId.Value, 3, DateTimeOffset.UtcNow), JsonContrato.Options)
        };
    }

    [RequiresDockerFact]
    public async Task Dado_FinalizacaoRecebida_Quando_WorkerProcessar_Entao_DeveSolicitarCobrancaComCommitAntesDoAckEEnvio()
    {
        // Arrange
        var mensagem = await PrepararFinalizacaoAsync();
        var sqs = new Mock<IAmazonSQS>();
        var confirmacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publicacao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consultas = 0;
        sqs.Setup(client => client.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (ReceiveMessageRequest _, CancellationToken ct) =>
            {
                if (Interlocked.Increment(ref consultas) == 1)
                {
                    return new ReceiveMessageResponse
                    {
                        HttpStatusCode = HttpStatusCode.OK,
                        Messages = [new Message
                        {
                            Body = JsonContrato.Serializar(mensagem), ReceiptHandle = "recibo-da-finalizacao",
                            Attributes = new Dictionary<string, string>
                            {
                                ["MessageGroupId"] = mensagem.OrdemServicoId.ToString("D"),
                                ["MessageDeduplicationId"] = mensagem.MensagemId.ToString("D")
                            }
                        }]
                    };
                }
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                throw new InvalidOperationException("O long polling de teste deve terminar por cancelamento.");
            });
        sqs.Setup(client => client.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (DeleteMessageRequest request, CancellationToken ct) =>
            {
                await using var verificacao = _fixture.CriarDbContext();
                (await verificacao.SagasOrdemServico.SingleAsync(ct)).Estado.Should().Be(EstadoSagaOrdemServico.AguardandoPagamento);
                (await verificacao.Set<RecebimentoMensagem>().CountAsync(ct)).Should().Be(1);
                (await verificacao.Set<RegistroOutboxSaga>().CountAsync(registro => registro.Tipo == "CriarCobranca", ct)).Should().Be(1);
                request.ReceiptHandle.Should().Be("recibo-da-finalizacao");
                confirmacao.TrySetResult();
                return new DeleteMessageResponse { HttpStatusCode = HttpStatusCode.OK };
            });
        sqs.Setup(client => client.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (SendMessageRequest request, CancellationToken ct) =>
            {
                await using var verificacao = _fixture.CriarDbContext();
                var pendente = await verificacao.Set<RegistroOutboxSaga>().SingleAsync(registro => registro.Tipo == "CriarCobranca", ct);
                request.QueueUrl.Should().Be(FilaOrcamento);
                request.MessageBody.Should().Be(pendente.Corpo);
                pendente.PublicadoEm.Should().BeNull();
                publicacao.TrySetResult();
                return new SendMessageResponse { HttpStatusCode = HttpStatusCode.OK, MessageId = "envio-confirmado" };
            });
        await using var services = CriarServicos(sqs.Object);
        var worker = services.GetServices<IHostedService>().OfType<ProcessamentoRemotoWorker>().Single();

        // Act
        await worker.StartAsync(default);
        try
        {
            await confirmacao.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await publicacao.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await AguardarPublicacaoPersistidaAsync();
        }
        finally
        {
            using var encerramento = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await worker.StopAsync(encerramento.Token);
        }

        // Assert
        sqs.Verify(client => client.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        sqs.Verify(client => client.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [RequiresDockerFact]
    public async Task Dado_ConsumoIndisponivelEEnvioIncerto_Quando_Retomar_Entao_DevePublicarSemPerderOutbox()
    {
        // Arrange
        var finalizacao = await PrepararFinalizacaoAsync();
        var sqs = new Mock<IAmazonSQS>();
        sqs.Setup(client => client.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Falha simulada do consumo"));
        sqs.SetupSequence(client => client.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Resposta do envio perdida"))
            .ReturnsAsync(new SendMessageResponse { HttpStatusCode = HttpStatusCode.OK, MessageId = "envio-confirmado" });
        await using var services = CriarServicos(sqs.Object);
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Application.GestaoOrdemServico.Interfaces.IProcessadorMensagem>()
                .ProcessarAsync(JsonContrato.Serializar(finalizacao));
        }
        var worker = services.GetServices<IHostedService>().OfType<ProcessamentoRemotoWorker>().Single();

        // Act
        await worker.StartAsync(default);
        try
        {
            await AguardarPublicacaoPersistidaAsync();
        }
        finally
        {
            using var encerramento = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await worker.StopAsync(encerramento.Token);
        }

        // Assert
        await using var dbContext = _fixture.CriarDbContext();
        var registro = await dbContext.Set<RegistroOutboxSaga>().SingleAsync(item => item.Tipo == "CriarCobranca");
        registro.Tentativas.Should().Be(1);
        registro.PublicadoEm.Should().NotBeNull();
        sqs.Verify(client => client.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        sqs.Verify(client => client.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task AguardarPublicacaoPersistidaAsync()
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await using var dbContext = _fixture.CriarDbContext();
            if (await dbContext.Set<RegistroOutboxSaga>().AnyAsync(registro => registro.Tipo == "CriarCobranca" && registro.PublicadoEm != null, limite.Token))
            {
                return;
            }
            await Task.Delay(20, limite.Token);
        }
    }
}
