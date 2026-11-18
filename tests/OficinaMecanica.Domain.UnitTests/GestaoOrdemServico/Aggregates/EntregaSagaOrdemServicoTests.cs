using FluentAssertions;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Domain.GestaoOrdemServico.Messages;
using OficinaMecanica.Domain.Shared.Exceptions;
using OficinaMecanica.Domain.UnitTests.GestaoOrdemServico.Factories;

namespace OficinaMecanica.Domain.UnitTests.GestaoOrdemServico.Aggregates;

public sealed class EntregaSagaOrdemServicoTests
{
    [Theory]
    [InlineData(EstadoSagaOrdemServico.Preparando)]
    [InlineData(EstadoSagaOrdemServico.AguardandoOrcamento)]
    [InlineData(EstadoSagaOrdemServico.AguardandoAprovacao)]
    [InlineData(EstadoSagaOrdemServico.ExecucaoAutorizada)]
    [InlineData(EstadoSagaOrdemServico.EmExecucao)]
    [InlineData(EstadoSagaOrdemServico.AguardandoPagamento)]
    public void Dado_SagaSemPagamentoConfirmado_Quando_ValidarEntrega_Entao_DeveBloquearSemAlterarEstado(EstadoSagaOrdemServico estado)
    {
        // Arrange
        var saga = SagaOrdemServicoTestDataFactory.Criar(estado);

        // Act
        var acao = () => saga.ValidarEntrega();

        // Assert
        acao.Should().Throw<DomainException>().WithMessage(SagaOrdemServicoErrorMessages.PagamentoNaoConfirmadoParaEntrega);
        saga.Estado.Should().Be(estado);
    }

    [Fact]
    public void Dado_CobrancaCriada_Quando_ValidarEntrega_Entao_IdentificadorNaoEquivaleAPagamento()
    {
        // Arrange
        var saga = SagaOrdemServicoTestDataFactory.Criar(EstadoSagaOrdemServico.AguardandoPagamento);
        saga.RegistrarCobranca(saga.OrcamentoId!.Value, 3, Guid.NewGuid(), 150m);

        // Act
        var acao = () => saga.ValidarEntrega();

        // Assert
        acao.Should().Throw<DomainException>().WithMessage(SagaOrdemServicoErrorMessages.PagamentoNaoConfirmadoParaEntrega);
        saga.Estado.Should().Be(EstadoSagaOrdemServico.AguardandoPagamento);
    }

    [Fact]
    public void Dado_PagamentoConfirmado_Quando_ValidarEntrega_Entao_DevePermitirSemReabrirSaga()
    {
        // Arrange
        var saga = SagaOrdemServicoTestDataFactory.Criar(EstadoSagaOrdemServico.PagamentoConfirmado);
        var pagamentoId = saga.PagamentoId;

        // Act
        var acao = () => saga.ValidarEntrega();

        // Assert
        acao.Should().NotThrow();
        saga.Estado.Should().Be(EstadoSagaOrdemServico.PagamentoConfirmado);
        saga.PagamentoId.Should().Be(pagamentoId);
    }
}
