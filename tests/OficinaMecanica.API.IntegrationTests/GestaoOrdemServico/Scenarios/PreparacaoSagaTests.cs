using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.API.IntegrationTests.Fixtures;
using OficinaMecanica.API.IntegrationTests.GestaoOrdemServico.Factories;
using OficinaMecanica.Application;
using OficinaMecanica.Application.Common;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarPreparacaoOrdemServico;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Infrastructure;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;

namespace OficinaMecanica.API.IntegrationTests.GestaoOrdemServico.Scenarios;

[Collection(OficinaMecanicaApiCollection.Nome)]
public sealed class PreparacaoSagaTests(OficinaMecanicaApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetarBancoAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [RequiresDockerFact]
    public async Task Dado_PreparacaoConfirmada_Quando_CatalogoMudarDuranteEspera_Entao_DeveUsarSnapshotOriginalUmaUnicaVez()
    {
        // Arrange
        var request = await CriarOrdemAsync();
        var sagaId = (await SolicitarAsync(request)).Valor;
        var comando = await ObterComandoAsync("PrepararExecucao");
        var dados = JsonContrato.Ler<PrepararExecucaoDados>(comando.Dados.GetRawText());
        await using (var contexto = fixture.CriarDbContext())
        {
            (await contexto.ServicosCatalogo.SingleAsync()).Atualizar("Descrição alterada após solicitação", 999m);
            await contexto.SaveChangesAsync();
        }
        var mensagem = Resultado(comando, "ExecucaoPreparada", new ExecucaoPreparadaDados(Guid.NewGuid(), dados.Versao,
            dados.Itens, new(dados.VeiculoId, dados.MecanicoId, dados.Servicos)));
        // Act
        await ProcessarAsync(mensagem);
        await ProcessarAsync(mensagem with { MensagemId = Guid.NewGuid() });
        var repeticao = await SolicitarAsync(request with { OperacaoId = Guid.NewGuid() });
        // Assert
        repeticao.Valor.Should().Be(sagaId);
        await using var verificacao = fixture.CriarDbContext();
        (await verificacao.SagasOrdemServico.SingleAsync()).Estado.Should().Be(EstadoSagaOrdemServico.AguardandoOrcamento);
        (await verificacao.Set<RegistroOutboxSaga>().CountAsync()).Should().Be(2);
        var gerar = await ObterComandoAsync("GerarOrcamento");
        gerar.CausationId.Should().Be(mensagem.MensagemId);
        var snapshot = JsonContrato.Ler<GerarOrcamentoDados>(gerar.Dados.GetRawText());
        snapshot.Servicos.Single().Descricao.Should().Be("Troca de óleo");
        snapshot.Servicos.Single().Valor.Should().Be(150m);
    }

    [RequiresDockerFact]
    public async Task Dado_RecusaDeEstoque_Quando_ReceberResultado_Entao_NaoDeveGerarOrcamentoOuConfirmarCompensacao()
    {
        // Arrange
        var request = await CriarOrdemAsync();
        await SolicitarAsync(request);
        var comando = await ObterComandoAsync("PrepararExecucao");
        var mensagem = Resultado(comando, "PreparacaoRecusada", new PreparacaoRecusadaDados(1, "ESTOQUE_INSUFICIENTE"));
        // Act
        await ProcessarAsync(mensagem);
        await ProcessarAsync(mensagem with { MensagemId = Guid.NewGuid() });
        // Assert
        await using var contexto = fixture.CriarDbContext();
        var saga = await contexto.SagasOrdemServico.SingleAsync();
        saga.Estado.Should().Be(EstadoSagaOrdemServico.PreparacaoRecusada);
        saga.PreparacaoCancelada.Should().BeFalse();
        (await contexto.Set<RegistroOutboxSaga>().CountAsync()).Should().Be(1);
    }

    [RequiresDockerFact]
    public async Task Dado_ResultadosDivergentes_Quando_Processar_Entao_DeveManterSagaSemEfeitosParciais()
    {
        // Arrange
        var request = await CriarOrdemAsync();
        await SolicitarAsync(request);
        var comando = await ObterComandoAsync("PrepararExecucao");
        var dados = JsonContrato.Ler<PrepararExecucaoDados>(comando.Dados.GetRawText());
        var resultado = new ExecucaoPreparadaDados(Guid.NewGuid(), 1, dados.Itens, new(dados.VeiculoId, dados.MecanicoId, dados.Servicos));
        var mensagem = Resultado(comando, "ExecucaoPreparada", resultado);
        // Act / Assert
        var divergentes = new[]
        {
            mensagem with { CausationId = Guid.NewGuid() },
            mensagem with { CorrelationId = "outra" },
            mensagem with { Dados = JsonSerializer.SerializeToElement(resultado with { Versao = 2 }, JsonContrato.Options) },
            mensagem with { Dados = JsonSerializer.SerializeToElement(resultado with { Diagnostico = resultado.Diagnostico with { MecanicoId = Guid.NewGuid() } }, JsonContrato.Options) },
            mensagem with { Dados = JsonSerializer.SerializeToElement(resultado with { Diagnostico = resultado.Diagnostico with { Servicos = [dados.Servicos[0] with { ServicoCatalogoId = Guid.NewGuid() }] } }, JsonContrato.Options) },
            mensagem with { Dados = JsonSerializer.SerializeToElement(resultado with { ItensReservados = [new(Guid.NewGuid(), 1)] }, JsonContrato.Options) }
        };
        foreach (var divergente in divergentes)
        {
            await FluentActions.Awaiting(() => ProcessarAsync(divergente)).Should().ThrowAsync<ContratoMensagemException>();
        }
        await using var contexto = fixture.CriarDbContext();
        (await contexto.SagasOrdemServico.SingleAsync()).Estado.Should().Be(EstadoSagaOrdemServico.Preparando);
        (await contexto.Set<RegistroOutboxSaga>().CountAsync()).Should().Be(1);
        (await contexto.Set<RecebimentoMensagem>().CountAsync()).Should().Be(1);
    }

    [RequiresDockerFact]
    public async Task Dado_LinhasDaMesmaPeca_Quando_SolicitarPreparacao_Entao_DeveSomarSomenteQuantidadeSemAlterarPrecos()
    {
        // Arrange
        var request = await CriarOrdemAsync();
        await using (var contexto = fixture.CriarDbContext())
        {
            var peca = Domain.Administrativo.Aggregates.PecaInsumoCatalogo.Criar("Óleo", Domain.Administrativo.Enums.TipoPecaInsumo.INSUMO, 999m);
            contexto.Add(peca);
            var ordem = await contexto.OrdensServico.Include(o => o.PecasInsumos).SingleAsync();
            ordem.RegistrarPecaInsumoNaAbertura(peca.Id, 2, 45m);
            ordem.RegistrarPecaInsumoNaAbertura(peca.Id, 1, 50m);
            await contexto.SaveChangesAsync();
        }
        // Act
        await SolicitarAsync(request);
        var comando = await ObterComandoAsync("PrepararExecucao");
        var dados = JsonContrato.Ler<PrepararExecucaoDados>(comando.Dados.GetRawText());
        await ProcessarAsync(Resultado(comando, "ExecucaoPreparada", new ExecucaoPreparadaDados(Guid.NewGuid(), 1,
            dados.Itens, new(dados.VeiculoId, dados.MecanicoId, dados.Servicos))));
        // Assert
        dados.Itens.Should().ContainSingle().Which.Quantidade.Should().Be(3);
        var gerar = await ObterComandoAsync("GerarOrcamento");
        var snapshot = JsonContrato.Ler<GerarOrcamentoDados>(gerar.Dados.GetRawText());
        snapshot.Itens.Should().HaveCount(2);
        snapshot.Itens.Sum(i => i.Quantidade * i.ValorUnitario).Should().Be(140m);
    }

    [RequiresDockerFact]
    public async Task Dado_OperacaoReutilizadaParaOutraOrdem_Quando_SolicitarPreparacao_Entao_DeveReverterSagaESnapshotEOutbox()
    {
        // Arrange
        var primeira = await CriarOrdemAsync();
        await SolicitarAsync(primeira);
        await using var contexto = fixture.CriarDbContext();
        var ordem = Domain.GestaoOrdemServico.Aggregates.OrdemServico.Abrir(2,
            (await contexto.Veiculos.SingleAsync()).Id, (await contexto.Mecanicos.SingleAsync()).Id);
        ordem.RegistrarServicoNaAbertura((await contexto.ServicosCatalogo.SingleAsync()).Id, 150m);
        contexto.Add(ordem);
        await contexto.SaveChangesAsync();
        // Act / Assert
        await FluentActions.Awaiting(() => SolicitarAsync(primeira with { OrdemServicoId = ordem.Id }))
            .Should().ThrowAsync<ContratoMensagemException>();
        await using var verificacao = fixture.CriarDbContext();
        (await verificacao.SagasOrdemServico.CountAsync()).Should().Be(1);
        (await verificacao.Set<RegistroOutboxSaga>().CountAsync()).Should().Be(1);
    }

    [RequiresDockerFact]
    public async Task Dado_EntradaInvalidaOuOrdemAusente_Quando_SolicitarPreparacao_Entao_NaoDevePersistirSaga()
    {
        // Act
        var invalida = await SolicitarAsync(new(Guid.Empty, Guid.Empty, Guid.Empty, ""));
        var ausente = await SolicitarAsync(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "teste"));
        // Assert
        invalida.Erro!.Tipo.Should().Be(TipoErro.Validacao);
        ausente.Erro!.Tipo.Should().Be(TipoErro.NaoEncontrado);
        await using var contexto = fixture.CriarDbContext();
        (await contexto.SagasOrdemServico.CountAsync()).Should().Be(0);
    }

    [RequiresDockerFact]
    public async Task Dado_SomaDeQuantidadesExcedente_Quando_SolicitarPreparacao_Entao_NaoDevePersistirEfeitos()
    {
        // Arrange
        var request = await CriarOrdemAsync();
        await using (var contexto = fixture.CriarDbContext())
        {
            var peca = Domain.Administrativo.Aggregates.PecaInsumoCatalogo.Criar("Parafuso", Domain.Administrativo.Enums.TipoPecaInsumo.PECA, 1m);
            contexto.Add(peca);
            var ordem = await contexto.OrdensServico.Include(o => o.PecasInsumos).SingleAsync();
            ordem.RegistrarPecaInsumoNaAbertura(peca.Id, int.MaxValue, 1m);
            ordem.RegistrarPecaInsumoNaAbertura(peca.Id, 1, 1m);
            await contexto.SaveChangesAsync();
        }
        // Act
        var resultado = await SolicitarAsync(request);
        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.Validacao);
        await using var verificacao = fixture.CriarDbContext();
        (await verificacao.SagasOrdemServico.CountAsync()).Should().Be(0);
        (await verificacao.Set<RegistroOutboxSaga>().CountAsync()).Should().Be(0);
    }

    [RequiresDockerFact]
    public async Task Dado_OrdemForaDaAbertura_Quando_SolicitarPreparacao_Entao_DeveRecusarSemCriarSaga()
    {
        // Arrange
        var request = await CriarOrdemAsync();
        await using (var contexto = fixture.CriarDbContext())
        {
            (await contexto.OrdensServico.SingleAsync()).IniciarDiagnostico();
            await contexto.SaveChangesAsync();
        }
        // Act
        var resultado = await SolicitarAsync(request);
        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.Validacao);
        await using var verificacao = fixture.CriarDbContext();
        (await verificacao.SagasOrdemServico.CountAsync()).Should().Be(0);
    }

    [RequiresDockerFact]
    public async Task Dado_ReentregaComOutraFilialOuCorrelacao_Quando_SolicitarPreparacao_Entao_NaoDeveMudarSnapshot()
    {
        // Arrange
        var request = await CriarOrdemAsync();
        await SolicitarAsync(request);
        var comando = await ObterComandoAsync("PrepararExecucao");
        // Act / Assert
        await FluentActions.Awaiting(() => SolicitarAsync(request with { FilialId = Guid.NewGuid() })).Should().ThrowAsync<ContratoMensagemException>();
        await FluentActions.Awaiting(() => SolicitarAsync(request with { CorrelationId = "outra" })).Should().ThrowAsync<ContratoMensagemException>();
        JsonContrato.Serializar(await ObterComandoAsync("PrepararExecucao")).Should().Be(JsonContrato.Serializar(comando));
    }

    [RequiresDockerFact]
    public async Task Dado_ConfirmacaoRecebida_Quando_ReentregarComValorEquivalenteOuCausacaoAdulterada_Entao_DeveDistinguirRepeticaoDeConflito()
    {
        // Arrange
        await SolicitarAsync(await CriarOrdemAsync());
        var comando = await ObterComandoAsync("PrepararExecucao");
        var dados = JsonContrato.Ler<PrepararExecucaoDados>(comando.Dados.GetRawText());
        var resultado = new ExecucaoPreparadaDados(Guid.NewGuid(), 1, dados.Itens, new(dados.VeiculoId, dados.MecanicoId, dados.Servicos));
        var mensagem = Resultado(comando, "ExecucaoPreparada", resultado);
        await ProcessarAsync(mensagem);
        // Act
        var equivalente = resultado with { Diagnostico = resultado.Diagnostico with { Servicos = [dados.Servicos[0] with { Valor = 150.00m }] } };
        await ProcessarAsync(mensagem with { MensagemId = Guid.NewGuid(), Dados = JsonSerializer.SerializeToElement(equivalente, JsonContrato.Options) });
        // Assert
        await FluentActions.Awaiting(() => ProcessarAsync(mensagem with { MensagemId = Guid.NewGuid(), CausationId = Guid.NewGuid() }))
            .Should().ThrowAsync<ContratoMensagemException>();
        await FluentActions.Awaiting(() => ProcessarAsync(mensagem with { MensagemId = Guid.NewGuid(), Dados = JsonSerializer.SerializeToElement(resultado with { PreparacaoId = Guid.NewGuid() }, JsonContrato.Options) }))
            .Should().ThrowAsync<ContratoMensagemException>();
        await using var contexto = fixture.CriarDbContext();
        (await contexto.Set<RegistroOutboxSaga>().CountAsync()).Should().Be(2);
    }

    private async Task<SolicitarPreparacaoOrdemServicoRequest> CriarOrdemAsync()
    {
        await using var contexto = fixture.CriarDbContext();
        var ordem = await SagaIntegracaoTestDataFactory.CriarAsync(contexto, criarSaga: false);
        return new(ordem.OrdemServicoId, Guid.NewGuid(), Guid.NewGuid(), ordem.CorrelationId);
    }

    private ServiceProvider CriarServicos()
    {
        using var contexto = fixture.CriarDbContext();
        return new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = contexto.Database.GetConnectionString() }).Build())
            .BuildServiceProvider();
    }

    private async Task<Result<Guid>> SolicitarAsync(SolicitarPreparacaoOrdemServicoRequest request)
    {
        await using var servicos = CriarServicos();
        await using var scope = servicos.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SolicitarPreparacaoOrdemServicoUseCase>().ExecuteAsync(request);
    }

    private async Task ProcessarAsync(MensagemIntegracao mensagem)
    {
        await using var servicos = CriarServicos();
        await using var scope = servicos.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProcessadorResultadoPreparacao>().ProcessarAsync(JsonContrato.Serializar(mensagem));
    }

    private async Task<MensagemIntegracao> ObterComandoAsync(string tipo)
    {
        await using var contexto = fixture.CriarDbContext();
        return JsonContrato.LerEnvelope((await contexto.Set<RegistroOutboxSaga>().SingleAsync(r => r.Tipo == tipo)).Corpo);
    }

    private static MensagemIntegracao Resultado(MensagemIntegracao comando, string tipo, object dados) =>
        new(Guid.NewGuid(), tipo, 1, comando.SagaId, comando.OrdemServicoId, comando.CorrelationId, comando.MensagemId,
            DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(dados, JsonContrato.Options));
}
