using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.API.IntegrationTests.Fixtures;
using OficinaMecanica.API.IntegrationTests.GestaoOrdemServico.Factories;
using OficinaMecanica.Application;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.CancelarOrdemServico;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Domain.Shared.Exceptions;
using OficinaMecanica.Infrastructure;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;

namespace OficinaMecanica.API.IntegrationTests.GestaoOrdemServico.Scenarios;

[Collection(OficinaMecanicaApiCollection.Nome)]
public sealed class CancelamentoDistribuidoTests(OficinaMecanicaApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetarBancoAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private ServiceProvider CriarServicos()
    {
        using var contexto = fixture.CriarDbContext();
        return new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = contexto.Database.GetConnectionString()
            }).Build()).BuildServiceProvider();
    }

    private async Task<Guid> PrepararAsync(bool iniciarFisicamente = false)
    {
        await using var contexto = fixture.CriarDbContext();
        await SagaIntegracaoTestDataFactory.CriarAsync(contexto);
        var saga = await contexto.SagasOrdemServico.SingleAsync();
        var comando = new MensagemIntegracao(Guid.NewGuid(), "PrepararExecucao", 1, saga.Id, saga.OrdemServicoId,
            "correlacao-cancelamento", Guid.NewGuid(), DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(new { Versao = 2 }, JsonContrato.Options));
        contexto.Add(RegistroOutboxSaga.Criar(comando, 2, "Execucao"));
        if (iniciarFisicamente)
        {
            saga.RegistrarOrcamento(Guid.NewGuid(), 3, 150m);
            saga.RegistrarDecisao(saga.OrcamentoId!.Value, 3, DecisaoOrcamento.Aprovado);
            saga.RegistrarInicioFisico(saga.PreparacaoId!.Value, 3);
        }
        await contexto.SaveChangesAsync();
        return saga.OrdemServicoId;
    }

    [RequiresDockerFact]
    public async Task Dado_PreparacaoConfirmada_Quando_SolicitarCancelamentoDuasVezes_Entao_DeveManterDoisComandosSemCompensacaoFicticia()
    {
        // Arrange
        var ordemId = await PrepararAsync();
        await using var servicos = CriarServicos();

        // Act
        for (var tentativa = 0; tentativa < 2; tentativa++)
        {
            await using var escopo = servicos.CreateAsyncScope();
            var resultado = await escopo.ServiceProvider.GetRequiredService<SolicitarCancelamentoOrdemServicoUseCase>()
                .ExecuteAsync(new(ordemId, MotivoCancelamentoOrdemServico.ReprovacaoOrcamento));
            resultado.Sucesso.Should().BeTrue();
        }

        // Assert
        await using var contexto = fixture.CriarDbContext();
        var saga = await contexto.SagasOrdemServico.SingleAsync();
        saga.Estado.Should().Be(EstadoSagaOrdemServico.CompensacaoPendente);
        saga.PreparacaoCancelada.Should().BeFalse();
        saga.OrcamentoCancelado.Should().BeFalse();
        (await contexto.Set<RegistroOutboxSaga>().CountAsync()).Should().Be(3);
        var cancelar = await contexto.Set<RegistroOutboxSaga>().SingleAsync(r => r.Tipo == "CancelarOrcamento");
        var mensagem = JsonContrato.LerEnvelope(cancelar.Corpo);
        mensagem.CorrelationId.Should().Be("correlacao-cancelamento");
        mensagem.Dados.GetProperty("versao").GetInt32().Should().Be(3);
        mensagem.Dados.TryGetProperty("orcamentoId", out _).Should().BeFalse();
        (await contexto.OrdensServico.SingleAsync()).Status.Should().Be(StatusOrdemServico.EmDiagnostico);
    }

    [RequiresDockerFact]
    public async Task Dado_DoisResultadosDeCancelamento_Quando_Confirmar_Entao_DeveCancelarOrdemSomenteAposAmbos()
    {
        // Arrange
        var ordemId = await PrepararAsync();
        await using var servicos = CriarServicos();
        await using (var escopo = servicos.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<SolicitarCancelamentoOrdemServicoUseCase>()
                .ExecuteAsync(new(ordemId, MotivoCancelamentoOrdemServico.ReprovacaoOrcamento));
        }
        await using var contexto = fixture.CriarDbContext();
        var saga = await contexto.SagasOrdemServico.SingleAsync();
        var origem = JsonContrato.LerEnvelope((await contexto.Set<RegistroOutboxSaga>()
            .SingleAsync(r => r.Tipo == "CancelarPreparacao")).Corpo);

        // Act
        var preparacao = origem with
        {
            MensagemId = Guid.NewGuid(),
            Tipo = "PreparacaoCancelada",
            CausationId = origem.MensagemId,
            Dados = JsonSerializer.SerializeToElement(new PreparacaoCanceladaDados(saga.PreparacaoId!.Value, 2), JsonContrato.Options)
        };
        await using (var escopo = servicos.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<IProcessadorMensagem>().ProcessarAsync(JsonContrato.Serializar(preparacao));
        }
        await using (var intermediario = fixture.CriarDbContext())
        {
            (await intermediario.OrdensServico.SingleAsync()).Status.Should().NotBe(StatusOrdemServico.Cancelada);
        }
        var orcamento = origem with
        {
            MensagemId = Guid.NewGuid(),
            Tipo = "OrcamentoCancelado",
            CausationId = Guid.NewGuid(),
            Dados = JsonSerializer.SerializeToElement(new OrcamentoCanceladoDados(null, 3), JsonContrato.Options)
        };
        await using (var escopo = servicos.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<IProcessadorMensagem>().ProcessarAsync(JsonContrato.Serializar(orcamento));
        }

        // Assert
        await using var verificacao = fixture.CriarDbContext();
        (await verificacao.SagasOrdemServico.SingleAsync()).Estado.Should().Be(EstadoSagaOrdemServico.Compensada);
        var ordem = await verificacao.OrdensServico.SingleAsync();
        ordem.Status.Should().Be(StatusOrdemServico.Cancelada);
        ordem.MotivoCancelamento.Should().Be(MotivoCancelamentoOrdemServico.ReprovacaoOrcamento);
    }

    [RequiresDockerFact]
    public async Task Dado_InicioFisicoConfirmado_Quando_SolicitarCancelamento_Entao_DeveRecusarSemPublicarLiberacao()
    {
        // Arrange
        var ordemId = await PrepararAsync(true);
        await using var servicos = CriarServicos();
        await using var escopo = servicos.CreateAsyncScope();

        // Act
        var acao = () => escopo.ServiceProvider.GetRequiredService<SolicitarCancelamentoOrdemServicoUseCase>()
            .ExecuteAsync(new(ordemId, MotivoCancelamentoOrdemServico.ReprovacaoOrcamento));

        // Assert
        await acao.Should().ThrowAsync<DomainException>();
        await using var contexto = fixture.CriarDbContext();
        (await contexto.Set<RegistroOutboxSaga>().CountAsync()).Should().Be(1);
        (await contexto.SagasOrdemServico.SingleAsync()).Estado.Should().Be(EstadoSagaOrdemServico.EmExecucao);
    }

    [RequiresDockerFact]
    public async Task Dado_MotivoDivergente_Quando_RepetirCancelamento_Entao_DeveRecusarSemDuplicarComandos()
    {
        // Arrange
        var ordemId = await PrepararAsync();
        await using var servicos = CriarServicos();
        await using (var escopo = servicos.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<SolicitarCancelamentoOrdemServicoUseCase>()
                .ExecuteAsync(new(ordemId, MotivoCancelamentoOrdemServico.ReprovacaoOrcamento));
        }
        await using var repeticao = servicos.CreateAsyncScope();

        // Act
        var acao = () => repeticao.ServiceProvider.GetRequiredService<SolicitarCancelamentoOrdemServicoUseCase>()
            .ExecuteAsync(new(ordemId, MotivoCancelamentoOrdemServico.EstoqueInsuficiente));

        // Assert
        await acao.Should().ThrowAsync<ContratoMensagemException>();
        await using var contexto = fixture.CriarDbContext();
        (await contexto.Set<RegistroOutboxSaga>().CountAsync()).Should().Be(3);
    }
}
