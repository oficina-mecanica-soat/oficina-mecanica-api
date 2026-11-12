using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OficinaMecanica.API.IntegrationTests.Fixtures;
using OficinaMecanica.Domain.Administrativo.Aggregates;
using OficinaMecanica.Domain.Atendimento.Aggregates;
using OficinaMecanica.Domain.Atendimento.ValueObjects;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;

namespace OficinaMecanica.API.IntegrationTests.GestaoOrdemServico.Scenarios;

[Collection(OficinaMecanicaApiCollection.Nome)]
public sealed class SagaOrdemServicoPersistenciaTests(OficinaMecanicaApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetarBancoAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [RequiresDockerFact]
    public async Task Dado_CompensacaoParcialPersistida_Quando_RecarregarEConfirmarRestante_Entao_DeveConcluirSemPerderHistorico()
    {
        // Arrange
        var saga = await CriarSagaPersistidaAsync();
        await using (var contexto = fixture.CriarDbContext())
        {
            var persistida = await contexto.SagasOrdemServico.SingleAsync();
            persistida.RegistrarPreparacao(Guid.NewGuid(), 2);
            persistida.IniciarCompensacao();
            persistida.ConfirmarCancelamentoPreparacao(persistida.PreparacaoId!.Value, 2);
            await contexto.SaveChangesAsync();
        }

        // Act
        await using (var contexto = fixture.CriarDbContext())
        {
            var retomada = await contexto.SagasOrdemServico.SingleAsync();
            retomada.Estado.Should().Be(EstadoSagaOrdemServico.CompensacaoPendente);
            retomada.ConfirmarCancelamentoOrcamento(3);
            await contexto.SaveChangesAsync();
        }

        // Assert
        await using var verificacao = fixture.CriarDbContext();
        var resultado = await verificacao.SagasOrdemServico.AsNoTracking().SingleAsync();
        resultado.Id.Should().Be(saga.Id);
        resultado.OrdemServicoId.Should().Be(saga.OrdemServicoId);
        resultado.Estado.Should().Be(EstadoSagaOrdemServico.Compensada);
        resultado.VersaoPreparacao.Should().Be(2);
        resultado.VersaoOrcamento.Should().Be(3);
        resultado.PreparacaoCancelada.Should().BeTrue();
        resultado.OrcamentoCancelado.Should().BeTrue();
    }

    [RequiresDockerFact]
    public async Task Dado_ConfirmacoesConcorrentes_Quando_SalvarEstadoDesatualizado_Entao_DeveBloquearEPermitirReconciliacao()
    {
        // Arrange
        await CriarSagaPersistidaAsync();
        await using (var contexto = fixture.CriarDbContext())
        {
            var saga = await contexto.SagasOrdemServico.SingleAsync();
            saga.RegistrarPreparacao(Guid.NewGuid(), 2);
            saga.IniciarCompensacao();
            await contexto.SaveChangesAsync();
        }

        await using var primeiro = fixture.CriarDbContext();
        await using var segundo = fixture.CriarDbContext();
        var cancelamentoEstoque = await primeiro.SagasOrdemServico.SingleAsync();
        var cancelamentoOrcamento = await segundo.SagasOrdemServico.SingleAsync();

        // Act
        cancelamentoEstoque.ConfirmarCancelamentoPreparacao(cancelamentoEstoque.PreparacaoId!.Value, 2);
        await primeiro.SaveChangesAsync();
        cancelamentoOrcamento.ConfirmarCancelamentoOrcamento(3);
        var salvarConcorrente = () => segundo.SaveChangesAsync();

        // Assert
        await salvarConcorrente.Should().ThrowAsync<DbUpdateConcurrencyException>();

        // Act
        await segundo.Entry(cancelamentoOrcamento).ReloadAsync();
        cancelamentoOrcamento.ConfirmarCancelamentoOrcamento(3);
        await segundo.SaveChangesAsync();

        // Assert
        await using var verificacao = fixture.CriarDbContext();
        var retomada = await verificacao.SagasOrdemServico.SingleAsync();
        retomada.Estado.Should().Be(EstadoSagaOrdemServico.Compensada);
        retomada.PreparacaoCancelada.Should().BeTrue();
        retomada.OrcamentoCancelado.Should().BeTrue();
    }

    [RequiresDockerFact]
    public async Task Dado_PagamentoConfirmado_Quando_RecarregarSaga_Entao_DevePreservarOrcamentoDecisaoEFinalizacao()
    {
        // Arrange
        var saga = await CriarSagaPersistidaAsync();
        var orcamentoId = Guid.NewGuid();
        var pagamentoId = Guid.NewGuid();
        var finalizadoEm = new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

        // Act
        await using (var contexto = fixture.CriarDbContext())
        {
            var persistida = await contexto.SagasOrdemServico.SingleAsync();
            persistida.RegistrarPreparacao(Guid.NewGuid(), 2);
            persistida.RegistrarOrcamento(orcamentoId, 3, 150.25m);
            persistida.RegistrarDecisao(orcamentoId, 3, DecisaoOrcamento.Aprovado);
            persistida.RegistrarInicioFisico(persistida.PreparacaoId!.Value, 3);
            persistida.RegistrarFinalizacao(persistida.PreparacaoId.Value, 3, finalizadoEm);
            persistida.RegistrarPagamento(orcamentoId, 3, pagamentoId, 150.25m);
            await contexto.SaveChangesAsync();
        }

        // Assert
        await using var verificacao = fixture.CriarDbContext();
        var resultado = await verificacao.SagasOrdemServico.SingleAsync();
        resultado.Id.Should().Be(saga.Id);
        resultado.Estado.Should().Be(EstadoSagaOrdemServico.PagamentoConfirmado);
        resultado.OrcamentoId.Should().Be(orcamentoId);
        resultado.ValorOrcamento.Should().Be(150.25m);
        resultado.Decisao.Should().Be(DecisaoOrcamento.Aprovado);
        resultado.PagamentoId.Should().Be(pagamentoId);
        resultado.FinalizadoEm.Should().Be(finalizadoEm);
        resultado.InicioFisicoConfirmado.Should().BeTrue();
    }

    [RequiresDockerFact]
    public async Task Dado_SagaExistente_Quando_InserirOutraParaMesmaOrdem_Entao_DevePreservarOwnerUnico()
    {
        // Arrange
        var saga = await CriarSagaPersistidaAsync();
        await using var contexto = fixture.CriarDbContext();
        contexto.SagasOrdemServico.Add(SagaOrdemServico.Iniciar(saga.OrdemServicoId, 2, 3));

        // Act
        var salvar = () => contexto.SaveChangesAsync();

        // Assert
        await salvar.Should().ThrowAsync<DbUpdateException>();
        await using var verificacao = fixture.CriarDbContext();
        (await verificacao.SagasOrdemServico.CountAsync()).Should().Be(1);
    }

    [RequiresDockerFact]
    public async Task Dado_OrdemInexistente_Quando_InserirSaga_Entao_DeveBloquearRegistroOrfao()
    {
        // Arrange
        await using var contexto = fixture.CriarDbContext();
        contexto.SagasOrdemServico.Add(SagaOrdemServico.Iniciar(Guid.NewGuid(), 1, 1));

        // Act
        var salvar = () => contexto.SaveChangesAsync();

        // Assert
        await salvar.Should().ThrowAsync<DbUpdateException>();
    }

    private async Task<SagaOrdemServico> CriarSagaPersistidaAsync()
    {
        await using var contexto = fixture.CriarDbContext();
        var cliente = Cliente.Criar(CpfCnpj.Criar("52998224725"), "Cliente de teste",
            Endereco.Criar("Rua de teste", "10", "Centro", "São Paulo", "01001000"),
            Telefone.Criar("11999999999"), Email.Criar("teste@example.com"));
        var veiculo = Veiculo.Criar(cliente.Id, Placa.Criar("ABC1D23"), "Marca", "Modelo", 2020);
        var mecanico = Mecanico.Criar("Mecânico de teste", "MEC-TESTE");
        var ordem = OrdemServico.Abrir(1, veiculo.Id, mecanico.Id);
        var saga = SagaOrdemServico.Iniciar(ordem.Id, 2, 3);
        contexto.AddRange(cliente, veiculo, mecanico, ordem, saga);
        await contexto.SaveChangesAsync();
        return saga;
    }
}
