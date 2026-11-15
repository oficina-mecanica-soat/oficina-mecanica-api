using FluentAssertions;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.FluxoDistribuido.IntegrationTests.Fixtures;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums;
using DecisaoOrcamento = OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums.DecisaoOrcamento;

namespace OficinaMecanica.FluxoDistribuido.IntegrationTests.Scenarios;

public sealed class JornadaOrdemServicoTests(AmbienteDistribuidoFixture fixture) : IClassFixture<AmbienteDistribuidoFixture>
{
    [Fact]
    public async Task Dado_OrdemPreparada_Quando_RepetirCancelamento_Entao_CompensaAmbosSemDuplicarLiberacao()
    {
        await using var jornada = await fixture.CriarJornadaAsync();
        await jornada.AbrirAsync();
        await jornada.ConvergirAsync();
        (await jornada.SaldoAsync()).Reservada.Should().Be(3);

        await jornada.SolicitarCancelamentoAsync();
        await jornada.SolicitarCancelamentoAsync();
        var pendente = await jornada.SagaAsync();
        pendente.Estado.Should().Be(EstadoSagaOrdemServico.CompensacaoPendente);
        pendente.PreparacaoCancelada.Should().BeFalse();
        pendente.OrcamentoCancelado.Should().BeFalse();
        await jornada.ConvergirAsync();
        await jornada.ConvergirAsync();

        var compensada = await jornada.SagaAsync();
        compensada.Estado.Should().Be(EstadoSagaOrdemServico.Compensada);
        compensada.PreparacaoCancelada.Should().BeTrue();
        compensada.OrcamentoCancelado.Should().BeTrue();
        (await jornada.StatusOrcamentoAsync()).Should().Be(StatusOrcamento.Cancelado);
        (await jornada.SaldoAsync()).Disponivel.Should().Be(10);
        (await jornada.SaldoAsync()).Reservada.Should().Be(0);
        jornada.Provedor.QuantidadeCriacoes.Should().Be(0);
    }

    [Fact]
    public async Task Dado_InicioFisicoConfirmado_Quando_Cancelar_Entao_RecusaEPreservaReservaEOrcamento()
    {
        await using var jornada = await fixture.CriarJornadaAsync();
        await jornada.AbrirAsync();
        await jornada.ConvergirAsync();
        await jornada.DecidirAsync(DecisaoOrcamento.Aprovado);
        await jornada.ConvergirAsync();
        await jornada.IniciarServicoAsync();
        await jornada.ConvergirAsync();

        await Assert.ThrowsAsync<Domain.Shared.Exceptions.DomainException>(() => jornada.SolicitarCancelamentoAsync());
        await Assert.ThrowsAsync<Domain.Shared.Exceptions.DomainException>(() => jornada.SolicitarCancelamentoAsync());
        await jornada.ConvergirAsync();

        var saga = await jornada.SagaAsync();
        saga.Estado.Should().Be(EstadoSagaOrdemServico.EmExecucao);
        saga.InicioFisicoConfirmado.Should().BeTrue();
        saga.PreparacaoCancelada.Should().BeFalse();
        saga.OrcamentoCancelado.Should().BeFalse();
        (await jornada.StatusOrcamentoAsync()).Should().Be(StatusOrcamento.Aprovado);
        (await jornada.SaldoAsync()).Disponivel.Should().Be(7);
        (await jornada.SaldoAsync()).Reservada.Should().Be(3);
        jornada.Provedor.QuantidadeCriacoes.Should().Be(0);
    }

    [Fact]
    public async Task Dado_OrdemComSaldo_Quando_AprovarExecutarEPagar_Entao_EntregaSemDuplicarReservaOuCobranca()
    {
        // Arrange — três serviços reais, com dois bancos SQL e DynamoDB Local.
        await using var jornada = await fixture.CriarJornadaAsync();
        await jornada.AbrirAsync();

        // Act — cada transporte reentrega o JSON e cada operação reabre seu escopo.
        await jornada.ConvergirAsync();
        (await jornada.SaldoAsync()).Reservada.Should().Be(3);
        await jornada.DecidirAsync(DecisaoOrcamento.Aprovado);
        await jornada.DecidirAsync(DecisaoOrcamento.Aprovado);
        await jornada.ConvergirAsync();
        (await jornada.SagaAsync()).InicioFisicoConfirmado.Should().BeFalse();
        await jornada.IniciarServicoAsync();
        await jornada.IniciarServicoAsync();
        await jornada.ConvergirAsync();
        await jornada.FinalizarExecucaoAsync();
        await jornada.FinalizarExecucaoAsync();
        await jornada.ConvergirAsync();
        (await jornada.AndamentoAsync()).PagamentoConfirmado.Should().BeFalse();
        await Assert.ThrowsAsync<Domain.Shared.Exceptions.DomainException>(() => jornada.EntregarAsync());
        await jornada.ConfirmarPagamentoAsync();
        await jornada.ConfirmarPagamentoAsync();
        await jornada.ConvergirAsync();
        var entregue = await jornada.EntregarAsync();

        // Assert — efeitos únicos e entrega após confirmação financeira.
        entregue.Status.Should().Be(nameof(StatusOrdemServico.Entregue));
        (await jornada.SagaAsync()).Estado.Should().Be(EstadoSagaOrdemServico.PagamentoConfirmado);
        (await jornada.SaldoAsync()).Disponivel.Should().Be(7);
        (await jornada.SaldoAsync()).Reservada.Should().Be(0);
        jornada.Provedor.QuantidadeCriacoes.Should().Be(1);
    }

    [Fact]
    public async Task Dado_OrdemPreparada_Quando_RecusarOrcamento_Entao_CompensaParticipantesERestauraSaldoUmaVez()
    {
        // Arrange
        await using var jornada = await fixture.CriarJornadaAsync();
        await jornada.AbrirAsync();
        await jornada.ConvergirAsync();
        (await jornada.SaldoAsync()).Reservada.Should().Be(3);

        // Act
        await jornada.DecidirAsync(DecisaoOrcamento.Recusado);
        await jornada.ConvergirAsync();
        await jornada.ConvergirAsync();

        // Assert
        var saga = await jornada.SagaAsync();
        saga.Estado.Should().Be(EstadoSagaOrdemServico.Compensada);
        saga.PreparacaoCancelada.Should().BeTrue();
        saga.OrcamentoCancelado.Should().BeTrue();
        (await jornada.StatusOrcamentoAsync()).Should().Be(StatusOrcamento.Cancelado);
        (await jornada.SaldoAsync()).Disponivel.Should().Be(10);
        (await jornada.SaldoAsync()).Reservada.Should().Be(0);
        jornada.Provedor.QuantidadeCriacoes.Should().Be(0);
    }

    [Fact]
    public async Task Dado_SaldoInsuficiente_Quando_AbrirOrdem_Entao_RecusaSemReservaParcialOuCobranca()
    {
        // Arrange
        await using var jornada = await fixture.CriarJornadaAsync();
        await jornada.AbrirAsync(quantidade: 11);

        // Act
        await jornada.ConvergirAsync();

        // Assert
        (await jornada.SagaAsync()).Estado.Should().Be(EstadoSagaOrdemServico.PreparacaoRecusada);
        (await jornada.SaldoAsync()).Disponivel.Should().Be(10);
        (await jornada.SaldoAsync()).Reservada.Should().Be(0);
        (await jornada.AndamentoAsync()).OrcamentoId.Should().BeNull();
        jornada.Provedor.QuantidadeCriacoes.Should().Be(0);
    }
}
