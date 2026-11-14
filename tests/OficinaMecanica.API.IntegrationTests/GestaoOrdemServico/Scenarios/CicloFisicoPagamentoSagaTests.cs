using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.API.IntegrationTests.Fixtures;
using OficinaMecanica.API.IntegrationTests.GestaoOrdemServico.Factories;
using OficinaMecanica.Application;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Domain.Shared.Exceptions;
using OficinaMecanica.Infrastructure;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;

namespace OficinaMecanica.API.IntegrationTests.GestaoOrdemServico.Scenarios;

[Collection(OficinaMecanicaApiCollection.Nome)]
public sealed class CicloFisicoPagamentoSagaTests : IAsyncLifetime
{
    private readonly OficinaMecanicaApiFixture _fixture;

    public CicloFisicoPagamentoSagaTests(OficinaMecanicaApiFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetarBancoAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private ServiceProvider CriarServicos()
    {
        using var dbContext = _fixture.CriarDbContext();
        return new ServiceCollection().AddLogging().AddApplication()
            .AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = dbContext.Database.GetConnectionString()
            }).Build()).BuildServiceProvider();
    }

    private async Task<SagaOrdemServico> PrepararAsync(bool autorizar = true)
    {
        await using var dbContext = _fixture.CriarDbContext();
        await SagaIntegracaoTestDataFactory.CriarAsync(dbContext);
        var saga = await dbContext.Set<SagaOrdemServico>().SingleAsync();
        saga.RegistrarOrcamento(Guid.NewGuid(), 3, 150m);
        saga.RegistrarDecisao(saga.OrcamentoId!.Value, 3, DecisaoOrcamento.Aprovado);
        if (autorizar)
        {
            var comando = Mensagem(saga, "AutorizarExecucao", new ExecucaoIniciadaDados(saga.PreparacaoId!.Value, 3));
            dbContext.Add(RegistroOutboxSaga.Criar(comando, 3, "Execucao"));
        }
        await dbContext.SaveChangesAsync();
        return saga;
    }

    private static MensagemIntegracao Mensagem(SagaOrdemServico saga, string tipo, object dados, Guid? causacao = null)
    {
        return new MensagemIntegracao(Guid.CreateVersion7(), tipo, 1, saga.Id, saga.OrdemServicoId,
            "correlacao-integracao", causacao ?? Guid.NewGuid(), DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(dados, JsonContrato.Options));
    }

    private async Task ProcessarExecucaoAsync(MensagemIntegracao mensagem)
    {
        await using var services = CriarServicos();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OficinaMecanica.Application.GestaoOrdemServico.Interfaces.IProcessadorMensagem>().ProcessarAsync(JsonContrato.Serializar(mensagem));
    }

    private async Task ProcessarPagamentoAsync(MensagemIntegracao mensagem)
    {
        await using var services = CriarServicos();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OficinaMecanica.Application.GestaoOrdemServico.Interfaces.IProcessadorMensagem>().ProcessarAsync(JsonContrato.Serializar(mensagem));
    }

    private async Task<Guid> FinalizarAsync(SagaOrdemServico saga)
    {
        await ProcessarExecucaoAsync(Mensagem(saga, "ExecucaoIniciada", new ExecucaoIniciadaDados(saga.PreparacaoId!.Value, 3)));
        await ProcessarExecucaoAsync(Mensagem(saga, "ExecucaoFinalizada", new ExecucaoFinalizadaDados(saga.PreparacaoId.Value, 3, DateTimeOffset.UtcNow)));
        await using var dbContext = _fixture.CriarDbContext();
        return (await dbContext.Set<RegistroOutboxSaga>().SingleAsync(o => o.Tipo == "CriarCobranca")).Id;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_InicioFisicoTardio_Quando_ReceberDuranteCompensacao_Entao_PersisteRecuperacaoSemConfirmarCancelamento(bool orcamentoJaCancelado)
    {
        // Arrange: o início venceu no Execução, mas seu evento ainda não chegou à OS.
        var saga = await PrepararAsync();
        await using (var contexto = _fixture.CriarDbContext())
        {
            (await contexto.SagasOrdemServico.SingleAsync()).IniciarCompensacao();
            await contexto.SaveChangesAsync();
        }
        if (orcamentoJaCancelado)
        {
            await ProcessarExecucaoAsync(Mensagem(saga, "OrcamentoCancelado", new OrcamentoCanceladoDados(saga.OrcamentoId, 3)));
        }
        var iniciada = Mensagem(saga, "ExecucaoIniciada", new ExecucaoIniciadaDados(saga.PreparacaoId!.Value, 3));
        var cancelada = Mensagem(saga, "PreparacaoCancelada", new PreparacaoCanceladaDados(saga.PreparacaoId.Value, 2));
        var finalizada = Mensagem(saga, "ExecucaoFinalizada", new ExecucaoFinalizadaDados(saga.PreparacaoId.Value, 3, DateTimeOffset.UtcNow));

        // Act: cada processamento usa outro escopo/DbContext e lê o estado persistido.
        await ProcessarExecucaoAsync(iniciada);
        await ProcessarExecucaoAsync(iniciada);
        await ProcessarExecucaoAsync(iniciada with { MensagemId = Guid.NewGuid() });
        await FluentActions.Awaiting(() => ProcessarExecucaoAsync(cancelada)).Should().ThrowAsync<DomainException>();
        await FluentActions.Awaiting(() => ProcessarExecucaoAsync(finalizada)).Should().ThrowAsync<DomainException>();

        // Assert: reconhecer o início não resolve automaticamente a recuperação pendente.
        await using var verificacao = _fixture.CriarDbContext();
        var atual = await verificacao.SagasOrdemServico.SingleAsync();
        atual.Estado.Should().Be(EstadoSagaOrdemServico.RecuperacaoPendente);
        atual.InicioFisicoConfirmado.Should().BeTrue();
        atual.PreparacaoCancelada.Should().BeFalse();
        atual.OrcamentoCancelado.Should().Be(orcamentoJaCancelado);
        atual.FinalizadoEm.Should().BeNull();
        (await verificacao.Set<RegistroOutboxSaga>().CountAsync()).Should().Be(1); // Só a autorização preparada no Arrange.
        (await verificacao.Set<RecebimentoMensagem>().CountAsync()).Should().Be(orcamentoJaCancelado ? 3 : 2);
        (await verificacao.Set<RecebimentoMensagem>().AnyAsync(r => r.Id == cancelada.MensagemId || r.Id == finalizada.MensagemId)).Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_CancelamentoPreparacaoConfirmado_Quando_ReceberInicioTardio_Entao_NaoReabreExecucaoNemConfirmaRecibo(bool compensacaoCompleta)
    {
        // Arrange: contraponto do cenário anterior, em que o cancelamento venceu.
        var saga = await PrepararAsync();
        await using (var contexto = _fixture.CriarDbContext())
        {
            (await contexto.SagasOrdemServico.SingleAsync()).IniciarCompensacao();
            await contexto.SaveChangesAsync();
        }
        await ProcessarExecucaoAsync(Mensagem(saga, "PreparacaoCancelada", new PreparacaoCanceladaDados(saga.PreparacaoId!.Value, 2)));
        if (compensacaoCompleta)
        {
            await ProcessarExecucaoAsync(Mensagem(saga, "OrcamentoCancelado", new OrcamentoCanceladoDados(saga.OrcamentoId, 3)));
        }
        var iniciada = Mensagem(saga, "ExecucaoIniciada", new ExecucaoIniciadaDados(saga.PreparacaoId.Value, 3));

        // Act / Assert: reentregas não convertem um resultado conflitante em sucesso.
        await FluentActions.Awaiting(() => ProcessarExecucaoAsync(iniciada)).Should().ThrowAsync<DomainException>();
        await FluentActions.Awaiting(() => ProcessarExecucaoAsync(iniciada with { MensagemId = Guid.NewGuid() })).Should().ThrowAsync<DomainException>();
        await using var verificacao = _fixture.CriarDbContext();
        var atual = await verificacao.SagasOrdemServico.SingleAsync();
        atual.Estado.Should().Be(compensacaoCompleta ? EstadoSagaOrdemServico.Compensada : EstadoSagaOrdemServico.CompensacaoPendente);
        atual.InicioFisicoConfirmado.Should().BeFalse();
        atual.PreparacaoCancelada.Should().BeTrue();
        atual.OrcamentoCancelado.Should().Be(compensacaoCompleta);
        (await verificacao.Set<RegistroOutboxSaga>().CountAsync()).Should().Be(1);
        (await verificacao.Set<RecebimentoMensagem>().CountAsync()).Should().Be(compensacaoCompleta ? 2 : 1);
    }

    [RequiresDockerFact]
    public async Task Dado_FinalizacaoFisicaConfirmada_Quando_Reentregar_Entao_DeveSolicitarUmaCobrancaPreservandoContexto()
    {
        // Arrange
        var saga = await PrepararAsync();
        var iniciada = Mensagem(saga, "ExecucaoIniciada", new ExecucaoIniciadaDados(saga.PreparacaoId!.Value, 3));
        var finalizada = Mensagem(saga, "ExecucaoFinalizada", new ExecucaoFinalizadaDados(saga.PreparacaoId.Value, 3, DateTimeOffset.UtcNow)) with
        {
            TraceContext = new() { ["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01" }
        };

        // Act
        await ProcessarExecucaoAsync(iniciada);
        await ProcessarExecucaoAsync(finalizada);
        await ProcessarExecucaoAsync(finalizada with { MensagemId = Guid.NewGuid() });
        await ProcessarExecucaoAsync(iniciada with { MensagemId = Guid.NewGuid() });

        // Assert
        await using var dbContext = _fixture.CriarDbContext();
        var atual = await dbContext.Set<SagaOrdemServico>().SingleAsync();
        atual.Estado.Should().Be(EstadoSagaOrdemServico.AguardandoPagamento);
        atual.FinalizadoEm.Should().Be(JsonContrato.Ler<ExecucaoFinalizadaDados>(finalizada.Dados.GetRawText()).FinalizadoEm);
        var comando = await dbContext.Set<RegistroOutboxSaga>().SingleAsync(o => o.Tipo == "CriarCobranca");
        var envelope = JsonContrato.LerEnvelope(comando.Corpo);
        envelope.CausationId.Should().Be(finalizada.MensagemId);
        envelope.TraceContext.Should().BeEquivalentTo(finalizada.TraceContext);
        JsonContrato.Ler<CriarCobrancaDados>(envelope.Dados.GetRawText()).Should().Be(new CriarCobrancaDados(saga.OrcamentoId!.Value, 3));
    }

    [RequiresDockerFact]
    public async Task Dado_FinalizacaoAntesDoInicio_Quando_Processar_Entao_NaoDeveCriarCobrancaOuConfirmarRecibo()
    {
        // Arrange
        var saga = await PrepararAsync();
        var finalizada = Mensagem(saga, "ExecucaoFinalizada", new ExecucaoFinalizadaDados(saga.PreparacaoId!.Value, 3, DateTimeOffset.UtcNow));

        // Act
        var tentativa = () => ProcessarExecucaoAsync(finalizada);

        // Assert
        await tentativa.Should().ThrowAsync<DomainException>();
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Set<SagaOrdemServico>().SingleAsync()).Estado.Should().Be(EstadoSagaOrdemServico.ExecucaoAutorizada);
        (await dbContext.Set<RegistroOutboxSaga>().CountAsync(o => o.Tipo == "CriarCobranca")).Should().Be(0);
        (await dbContext.Set<RecebimentoMensagem>().CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("preparacao")]
    [InlineData("versao")]
    [InlineData("correlacao")]
    [InlineData("saga")]
    public async Task Dado_ResultadoFisicoDivergente_Quando_Processar_Entao_DeveRejeitar(string campo)
    {
        // Arrange
        var saga = await PrepararAsync();
        var mensagem = Mensagem(saga, "ExecucaoIniciada", new ExecucaoIniciadaDados(
            campo == "preparacao" ? Guid.NewGuid() : saga.PreparacaoId!.Value, campo == "versao" ? 2 : 3));
        if (campo == "correlacao")
        {
            mensagem = mensagem with { CorrelationId = "outra" };
        }
        if (campo == "saga")
        {
            mensagem = mensagem with { SagaId = Guid.NewGuid() };
        }

        // Act
        var tentativa = () => ProcessarExecucaoAsync(mensagem);

        // Assert
        await tentativa.Should().ThrowAsync<ContratoMensagemException>();
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Set<SagaOrdemServico>().SingleAsync()).InicioFisicoConfirmado.Should().BeFalse();
    }

    [RequiresDockerFact]
    public async Task Dado_AutorizacaoNaoEmitida_Quando_ReceberInicio_Entao_DeveRejeitar()
    {
        // Arrange
        var saga = await PrepararAsync(false);

        // Act
        var tentativa = () => ProcessarExecucaoAsync(Mensagem(saga, "ExecucaoIniciada", new ExecucaoIniciadaDados(saga.PreparacaoId!.Value, 3)));

        // Assert
        await tentativa.Should().ThrowAsync<ContratoMensagemException>();
    }

    [RequiresDockerFact]
    public async Task Dado_FalhaAoGravarComando_Quando_Finalizar_Entao_DeveReverterEstadoERecibo()
    {
        // Arrange
        var saga = await PrepararAsync();
        var iniciada = Mensagem(saga, "ExecucaoIniciada", new ExecucaoIniciadaDados(saga.PreparacaoId!.Value, 3));
        await ProcessarExecucaoAsync(iniciada);
        await using (var dbContext = _fixture.CriarDbContext())
        {
            dbContext.Add(RegistroOutboxSaga.Criar(Mensagem(saga, "CriarCobranca", new CriarCobrancaDados(saga.OrcamentoId!.Value, 3)), 3, "OrcamentoPagamento"));
            await dbContext.SaveChangesAsync();
        }

        // Act
        var tentativa = () => ProcessarExecucaoAsync(Mensagem(saga, "ExecucaoFinalizada", new ExecucaoFinalizadaDados(saga.PreparacaoId.Value, 3, DateTimeOffset.UtcNow)));

        // Assert
        await tentativa.Should().ThrowAsync<DbUpdateException>();
        await using var verificacao = _fixture.CriarDbContext();
        var atual = await verificacao.Set<SagaOrdemServico>().SingleAsync();
        atual.Estado.Should().Be(EstadoSagaOrdemServico.EmExecucao);
        atual.FinalizadoEm.Should().BeNull();
        (await verificacao.Set<RecebimentoMensagem>().CountAsync()).Should().Be(1);
    }

    [RequiresDockerFact]
    public async Task Dado_PagamentoVerificado_Quando_ConfirmarEReentregar_Entao_DeveConcluirSagaSemRegressao()
    {
        // Arrange
        var saga = await PrepararAsync();
        var comandoId = await FinalizarAsync(saga);
        var dados = new ResultadoPagamentoDados(Guid.NewGuid(), saga.OrcamentoId!.Value, 3, 150m);
        var confirmacao = Mensagem(saga, "PagamentoConfirmado", dados);

        // Act
        await ProcessarPagamentoAsync(Mensagem(saga, "PagamentoRecusado", dados));
        await ProcessarPagamentoAsync(confirmacao);
        await ProcessarPagamentoAsync(confirmacao with { MensagemId = Guid.NewGuid() });
        await ProcessarPagamentoAsync(Mensagem(saga, "CobrancaCriada", dados, comandoId));

        // Assert
        await using var dbContext = _fixture.CriarDbContext();
        var atual = await dbContext.Set<SagaOrdemServico>().SingleAsync();
        atual.Estado.Should().Be(EstadoSagaOrdemServico.PagamentoConfirmado);
        atual.PagamentoId.Should().Be(dados.PagamentoId);
        (await dbContext.Set<RegistroOutboxSaga>().CountAsync(o => o.Tipo == "CriarCobranca")).Should().Be(1);
    }

    [Theory]
    [InlineData("valor")]
    [InlineData("orcamento")]
    [InlineData("versao")]
    [InlineData("correlacao")]
    [InlineData("causacao")]
    public async Task Dado_ResultadoFinanceiroDivergente_Quando_Processar_Entao_DevePreservarSaga(string campo)
    {
        // Arrange
        var saga = await PrepararAsync();
        await FinalizarAsync(saga);
        var dados = new ResultadoPagamentoDados(Guid.NewGuid(), campo == "orcamento" ? Guid.NewGuid() : saga.OrcamentoId!.Value,
            campo == "versao" ? 2 : 3, campo == "valor" ? 149m : 150m);
        var mensagem = Mensagem(saga, campo == "causacao" ? "CobrancaCriada" : "PagamentoConfirmado", dados);
        if (campo == "correlacao")
        {
            mensagem = mensagem with { CorrelationId = "outra" };
        }

        // Act
        var tentativa = () => ProcessarPagamentoAsync(mensagem);

        // Assert
        await tentativa.Should().ThrowAsync<ContratoMensagemException>();
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Set<SagaOrdemServico>().SingleAsync()).Estado.Should().Be(EstadoSagaOrdemServico.AguardandoPagamento);
    }

    [RequiresDockerFact]
    public async Task Dado_CobrancaNaoSolicitada_Quando_ReceberPagamento_Entao_NaoDevePularExecucaoFisica()
    {
        // Arrange
        var saga = await PrepararAsync();
        var mensagem = Mensagem(saga, "PagamentoConfirmado", new ResultadoPagamentoDados(Guid.NewGuid(), saga.OrcamentoId!.Value, 3, 150m));

        // Act
        var tentativa = () => ProcessarPagamentoAsync(mensagem);

        // Assert
        await tentativa.Should().ThrowAsync<ContratoMensagemException>();
        await using var dbContext = _fixture.CriarDbContext();
        (await dbContext.Set<SagaOrdemServico>().SingleAsync()).PagamentoId.Should().BeNull();
    }

    [RequiresDockerFact]
    public async Task Dado_CobrancaCriada_Quando_ConfirmacaoChegarDepois_Entao_DevePersistirIdentidadeEAvancarSomenteNaConfirmacao()
    {
        // Arrange
        var saga = await PrepararAsync();
        var comandoId = await FinalizarAsync(saga);
        var dados = new ResultadoPagamentoDados(Guid.NewGuid(), saga.OrcamentoId!.Value, 3, 150m);

        // Act
        await ProcessarPagamentoAsync(Mensagem(saga, "CobrancaCriada", dados, comandoId));
        await using (var intermediario = _fixture.CriarDbContext())
        {
            var aguardando = await intermediario.SagasOrdemServico.SingleAsync();
            aguardando.PagamentoId.Should().Be(dados.PagamentoId);
            aguardando.Estado.Should().Be(EstadoSagaOrdemServico.AguardandoPagamento);
        }
        await ProcessarPagamentoAsync(Mensagem(saga, "PagamentoConfirmado", dados));

        // Assert
        await using var verificacao = _fixture.CriarDbContext();
        var confirmada = await verificacao.SagasOrdemServico.SingleAsync();
        confirmada.PagamentoId.Should().Be(dados.PagamentoId);
        confirmada.Estado.Should().Be(EstadoSagaOrdemServico.PagamentoConfirmado);
    }

    [RequiresDockerFact]
    public async Task Dado_CobrancaConhecida_Quando_OutraIdentidadeForConfirmada_Entao_DeveRejeitarSemConfirmarRecibo()
    {
        // Arrange
        var saga = await PrepararAsync();
        var comandoId = await FinalizarAsync(saga);
        var dados = new ResultadoPagamentoDados(Guid.NewGuid(), saga.OrcamentoId!.Value, 3, 150m);
        await ProcessarPagamentoAsync(Mensagem(saga, "CobrancaCriada", dados, comandoId));
        int recibos;
        await using (var inicial = _fixture.CriarDbContext())
        {
            recibos = await inicial.Set<RecebimentoMensagem>().CountAsync();
        }

        // Act
        var tentativa = () => ProcessarPagamentoAsync(Mensagem(saga, "PagamentoConfirmado", dados with { PagamentoId = Guid.NewGuid() }));

        // Assert
        await tentativa.Should().ThrowAsync<DomainException>();
        await using var verificacao = _fixture.CriarDbContext();
        var aguardando = await verificacao.SagasOrdemServico.SingleAsync();
        aguardando.Estado.Should().Be(EstadoSagaOrdemServico.AguardandoPagamento);
        aguardando.PagamentoId.Should().Be(dados.PagamentoId);
        (await verificacao.Set<RecebimentoMensagem>().CountAsync()).Should().Be(recibos);
    }
}
