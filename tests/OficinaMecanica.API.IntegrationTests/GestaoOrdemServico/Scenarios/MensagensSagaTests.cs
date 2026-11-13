using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.API.IntegrationTests.Fixtures;
using OficinaMecanica.API.IntegrationTests.GestaoOrdemServico.Factories;
using OficinaMecanica.Application;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoDaOrdemServico;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Infrastructure;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;
namespace OficinaMecanica.API.IntegrationTests.GestaoOrdemServico.Scenarios;

[Collection(OficinaMecanicaApiCollection.Nome)]
public sealed class MensagensSagaTests(OficinaMecanicaApiFixture fixture) : IAsyncLifetime
{
    [RequiresDockerFact]
    public async Task Dado_LinhasDaMesmaPecaComPrecosDistintos_Quando_ProduzirSnapshot_Entao_NaoDeveAgruparOuReprecificar()
    {
        // Arrange
        await using var contexto = fixture.CriarDbContext();
        var request = await SagaIntegracaoTestDataFactory.CriarAsync(contexto);
        var catalogo = Domain.Administrativo.Aggregates.PecaInsumoCatalogo.Criar("Óleo", Domain.Administrativo.Enums.TipoPecaInsumo.INSUMO, 999m);
        contexto.PecasInsumosCatalogo.Add(catalogo);
        var ordem = await contexto.OrdensServico.Include(o => o.PecasInsumos).SingleAsync();
        ordem.ReservarPecaInsumo(catalogo.Id, 2, 45m);
        ordem.ReservarPecaInsumo(catalogo.Id, 1, 50m);
        await contexto.SaveChangesAsync();
        // Act
        await using var servicos = CriarServicos();
        await using var scope = servicos.CreateAsyncScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<SolicitarOrcamentoDaOrdemServicoUseCase>()
            .ExecuteAsync(new(request.SagaId, request.OrdemServicoId, request.OperacaoId, request.CorrelationId));
        // Assert
        resultado.Sucesso.Should().BeTrue();
        await using var verificacao = fixture.CriarDbContext();
        var snapshot = JsonContrato.Ler<GerarOrcamentoDados>(JsonContrato.LerEnvelope((await verificacao.Set<RegistroOutboxSaga>().SingleAsync()).Corpo).Dados.GetRawText());
        snapshot.Itens.Should().HaveCount(2);
        snapshot.Itens.Select(i => i.ValorUnitario).Should().BeEquivalentTo([45m, 50m]);
        snapshot.Itens.Sum(i => i.Quantidade * i.ValorUnitario).Should().Be(140m);
    }
    [RequiresDockerFact]
    public async Task Dado_OrdemComValoresCongelados_Quando_SolicitarOrcamento_Entao_DeveUsarClienteEValoresDaOrdem()
    {
        // Arrange
        await using var origem = fixture.CriarDbContext();
        var request = await SagaIntegracaoTestDataFactory.CriarAsync(origem);
        var catalogo = await origem.ServicosCatalogo.SingleAsync();
        catalogo.Atualizar("Descrição atualizada", 999m);
        await origem.SaveChangesAsync();

        // Act
        await using (var servicos = CriarServicos())
        await using (var scope = servicos.CreateAsyncScope())
        {
            var resultado = await scope.ServiceProvider.GetRequiredService<SolicitarOrcamentoDaOrdemServicoUseCase>()
                .ExecuteAsync(new(request.SagaId, request.OrdemServicoId, request.OperacaoId, request.CorrelationId));
            resultado.Sucesso.Should().BeTrue();
        }

        // Assert
        await using var verificacao = fixture.CriarDbContext();
        var comando = await verificacao.Set<RegistroOutboxSaga>().SingleAsync();
        var snapshot = JsonContrato.Ler<GerarOrcamentoDados>(JsonContrato.LerEnvelope(comando.Corpo).Dados.GetRawText());
        snapshot.ClienteId.Should().Be(request.Dados.ClienteId);
        snapshot.Servicos.Should().ContainSingle().Which.Valor.Should().Be(150m);
        snapshot.Servicos.Single().Descricao.Should().Be("Descrição atualizada");
        snapshot.Servicos.Single().ServicoId.Should().Be(request.Dados.Servicos.Single().ServicoId);
        snapshot.Servicos.Single().ServicoCatalogoId.Should().Be(catalogo.Id);

        // Arrange: a alteração posterior do catálogo não pode reescrever o snapshot enviado.
        catalogo.Atualizar("Outra descrição", 777m);
        await origem.SaveChangesAsync();
        await using (var servicos = CriarServicos())
        await using (var scope = servicos.CreateAsyncScope())
        {
            var repeticao = await scope.ServiceProvider.GetRequiredService<SolicitarOrcamentoDaOrdemServicoUseCase>()
                .ExecuteAsync(new(request.SagaId, request.OrdemServicoId, Guid.NewGuid(), request.CorrelationId));
            repeticao.Valor.Should().Be(comando.Id);
        }
        await using var final = fixture.CriarDbContext();
        (await final.Set<RegistroOutboxSaga>().SingleAsync()).Corpo.Should().Be(comando.Corpo);
    }

    [RequiresDockerFact]
    public async Task Dado_SagaAusenteOuEntradaInvalida_Quando_ConstruirSnapshot_Entao_NaoDeveGerarComando()
    {
        // Arrange
        await using var servicos = CriarServicos();
        await using var scope = servicos.CreateAsyncScope();
        var caso = scope.ServiceProvider.GetRequiredService<SolicitarOrcamentoDaOrdemServicoUseCase>();
        // Act
        var invalida = await caso.ExecuteAsync(new(Guid.Empty, Guid.Empty, Guid.Empty, ""));
        var ausente = await caso.ExecuteAsync(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "consulta"));
        // Assert
        invalida.Erro!.Tipo.Should().Be(Application.Common.TipoErro.Validacao);
        ausente.Erro!.Tipo.Should().Be(Application.Common.TipoErro.NaoEncontrado);
        await using var verificacao = fixture.CriarDbContext();
        (await verificacao.Set<RegistroOutboxSaga>().CountAsync()).Should().Be(0);
    }
    [RequiresDockerFact]
    public async Task Dado_TotalDiferenteDoSnapshotSolicitado_Quando_ReceberOrcamento_Entao_NaoDeveAvancarSaga()
    {
        // Arrange
        var request = await IniciarAsync();
        await using var inicial = fixture.CriarDbContext();
        var comando = await inicial.Set<RegistroOutboxSaga>().SingleAsync();
        var resultado = Resultado(request, "OrcamentoGerado", new OrcamentoGeradoDados(Guid.NewGuid(), 3, 151m), comando.Id);
        // Act / Assert
        await FluentActions.Awaiting(() => ProcessarAsync(resultado)).Should().ThrowAsync<ContratoMensagemException>();
        await using var verificacao = fixture.CriarDbContext();
        (await verificacao.SagasOrdemServico.SingleAsync()).Estado.Should().Be(EstadoSagaOrdemServico.AguardandoOrcamento);
        (await verificacao.Set<RecebimentoMensagem>().CountAsync()).Should().Be(1);
    }
    public Task InitializeAsync() => fixture.ResetarBancoAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private ServiceProvider CriarServicos()
    {
        using var contexto = fixture.CriarDbContext();
        return new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = contexto.Database.GetConnectionString() }).Build())
            .BuildServiceProvider();
    }
    private async Task<SolicitarOrcamentoSagaRequest> IniciarAsync()
    {
        await using var contexto = fixture.CriarDbContext();
        var request = await SagaIntegracaoTestDataFactory.CriarAsync(contexto);
        await SolicitarAsync(request);
        return request;
    }
    private async Task<Guid> SolicitarAsync(SolicitarOrcamentoSagaRequest request)
    {
        await using var servicos = CriarServicos();
        await using var scope = servicos.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<SolicitarOrcamentoSagaUseCase>().ExecuteAsync(request);
        result.Sucesso.Should().BeTrue();
        return result.Valor;
    }
    private async Task ProcessarAsync(MensagemIntegracao mensagem)
    {
        await using var servicos = CriarServicos();
        await using var scope = servicos.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProcessadorResultadoOrcamento>().ProcessarAsync(JsonContrato.Serializar(mensagem));
    }
    private static MensagemIntegracao Resultado(SolicitarOrcamentoSagaRequest request, string tipo, object dados, Guid causacao) =>
        new(Guid.NewGuid(), tipo, 1, request.SagaId, request.OrdemServicoId, request.CorrelationId, causacao, DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(dados, JsonContrato.Options));
    private async Task<MensagemIntegracao> GerarAsync(SolicitarOrcamentoSagaRequest request)
    {
        await using var contexto = fixture.CriarDbContext();
        var comando = await contexto.Set<RegistroOutboxSaga>().SingleAsync(r => r.Tipo == "GerarOrcamento");
        var gerado = Resultado(request, "OrcamentoGerado", new OrcamentoGeradoDados(Guid.NewGuid(), 3, 150m), comando.Id);
        await ProcessarAsync(gerado);
        return gerado;
    }
    [RequiresDockerFact]
    public async Task Dado_SolicitacaoRepetida_Quando_Reiniciar_Entao_DeveReutilizarComandoOriginal()
    {
        // Arrange
        var request = await IniciarAsync();
        await using var inicial = fixture.CriarDbContext();
        var comando = await inicial.Set<RegistroOutboxSaga>().SingleAsync();
        // Act
        var id = await SolicitarAsync(request with { OperacaoId = Guid.NewGuid() });
        // Assert
        id.Should().Be(comando.Id);
        await using var contexto = fixture.CriarDbContext();
        (await contexto.Set<RegistroOutboxSaga>().SingleAsync()).Corpo.Should().Be(comando.Corpo);
        (await contexto.Set<RecebimentoMensagem>().CountAsync()).Should().Be(2);
    }
    [RequiresDockerFact]
    public async Task Dado_Aprovacao_Quando_ReceberDuplicatas_Entao_DeveAutorizarUmaVezSemIniciarReparoFisico()
    {
        // Arrange
        var request = await IniciarAsync();
        var gerado = await GerarAsync(request);
        var orcamento = JsonContrato.Ler<OrcamentoGeradoDados>(gerado.Dados.GetRawText());
        var decisao = Resultado(request, "OrcamentoDecidido", new OrcamentoDecididoDados(orcamento.OrcamentoId, 3, "Aprovado"), Guid.NewGuid());
        // Act
        await ProcessarAsync(decisao);
        await ProcessarAsync(decisao);
        await ProcessarAsync(decisao with { MensagemId = Guid.NewGuid() });
        await ProcessarAsync(gerado with { MensagemId = Guid.NewGuid() });
        // Assert
        await using var contexto = fixture.CriarDbContext();
        var saga = await contexto.SagasOrdemServico.SingleAsync();
        saga.Estado.Should().Be(EstadoSagaOrdemServico.ExecucaoAutorizada);
        saga.InicioFisicoConfirmado.Should().BeFalse();
        var comando = await contexto.Set<RegistroOutboxSaga>().SingleAsync(r => r.Tipo == "AutorizarExecucao");
        comando.Destino.Should().Be("Execucao");
        var mensagem = JsonContrato.LerEnvelope(comando.Corpo);
        mensagem.CausationId.Should().Be(decisao.MensagemId);
        mensagem.Dados.GetProperty("versaoOrcamento").GetInt32().Should().Be(3);
        mensagem.Dados.GetProperty("preparacaoId").GetGuid().Should().Be(saga.PreparacaoId!.Value);
        saga.VersaoPreparacao.Should().Be(2);
    }
    [RequiresDockerFact]
    public async Task Dado_Recusa_Quando_ConfirmarCompensacoesSeparadas_Entao_DeveAguardarAmbas()
    {
        // Arrange
        var request = await IniciarAsync();
        var gerado = await GerarAsync(request);
        var orcamento = JsonContrato.Ler<OrcamentoGeradoDados>(gerado.Dados.GetRawText());
        var decisao = Resultado(request, "OrcamentoDecidido", new OrcamentoDecididoDados(orcamento.OrcamentoId, 3, "Recusado"), Guid.NewGuid());
        await ProcessarAsync(decisao);
        await using var inicial = fixture.CriarDbContext();
        var saga = await inicial.SagasOrdemServico.SingleAsync();
        saga.Estado.Should().Be(EstadoSagaOrdemServico.CompensacaoPendente);
        (await inicial.Set<RegistroOutboxSaga>().CountAsync(r => r.Tipo == "AutorizarExecucao")).Should().Be(0);
        (await inicial.Set<RegistroOutboxSaga>().CountAsync(r => r.Tipo.StartsWith("Cancelar"))).Should().Be(2);
        // Act
        var cancelado = Resultado(request, "OrcamentoCancelado", new OrcamentoCanceladoDados(orcamento.OrcamentoId, 3), Guid.NewGuid());
        await ProcessarAsync(cancelado);
        await using (var parcial = fixture.CriarDbContext())
        { (await parcial.SagasOrdemServico.SingleAsync()).Estado.Should().Be(EstadoSagaOrdemServico.CompensacaoPendente); }
        await ProcessarAsync(Resultado(request, "PreparacaoCancelada", new PreparacaoCanceladaDados(saga.PreparacaoId!.Value, 2), Guid.NewGuid()));
        await ProcessarAsync(cancelado);
        // Assert
        await using var contexto = fixture.CriarDbContext();
        (await contexto.SagasOrdemServico.SingleAsync()).Estado.Should().Be(EstadoSagaOrdemServico.Compensada);
    }
    [RequiresDockerFact]
    public async Task Dado_IdentidadeReutilizadaComOutroResultado_Quando_Processar_Entao_NaoDeveSobrescreverDecisao()
    {
        // Arrange
        var request = await IniciarAsync();
        var gerado = await GerarAsync(request);
        var orcamento = JsonContrato.Ler<OrcamentoGeradoDados>(gerado.Dados.GetRawText());
        var aprovado = Resultado(request, "OrcamentoDecidido", new OrcamentoDecididoDados(orcamento.OrcamentoId, 3, "Aprovado"), Guid.NewGuid());
        await ProcessarAsync(aprovado);
        var recusado = aprovado with { Dados = JsonSerializer.SerializeToElement(new OrcamentoDecididoDados(orcamento.OrcamentoId, 3, "Recusado"), JsonContrato.Options) };
        // Act / Assert
        await FluentActions.Awaiting(() => ProcessarAsync(recusado)).Should().ThrowAsync<ContratoMensagemException>();
        await using var contexto = fixture.CriarDbContext();
        (await contexto.SagasOrdemServico.SingleAsync()).Estado.Should().Be(EstadoSagaOrdemServico.ExecucaoAutorizada);
        (await contexto.Set<RegistroOutboxSaga>().CountAsync()).Should().Be(2);
    }
    [RequiresDockerFact]
    public async Task Dado_VersaoOuCausacaoIncompativel_Quando_ReceberResultado_Entao_NaoDeveConfirmarRecebimento()
    {
        // Arrange
        var request = await IniciarAsync();
        var resultado = Resultado(request, "OrcamentoGerado", new OrcamentoGeradoDados(Guid.NewGuid(), 2, 150m), Guid.NewGuid());
        // Act / Assert
        await FluentActions.Awaiting(() => ProcessarAsync(resultado)).Should().ThrowAsync<ContratoMensagemException>();
        resultado = resultado with { Dados = JsonSerializer.SerializeToElement(new OrcamentoGeradoDados(Guid.NewGuid(), 3, 150m), JsonContrato.Options) };
        await FluentActions.Awaiting(() => ProcessarAsync(resultado)).Should().ThrowAsync<ContratoMensagemException>();
        await using var contexto = fixture.CriarDbContext();
        (await contexto.Set<RecebimentoMensagem>().CountAsync()).Should().Be(1);
        (await contexto.SagasOrdemServico.SingleAsync()).Estado.Should().Be(EstadoSagaOrdemServico.AguardandoOrcamento);
    }
}
