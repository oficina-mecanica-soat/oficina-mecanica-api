using FluentAssertions;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Domain.Shared.Exceptions;
using OficinaMecanica.Domain.UnitTests.GestaoOrdemServico.Factories;

namespace OficinaMecanica.Domain.UnitTests.GestaoOrdemServico.Aggregates;

public sealed class CobrancaSagaOrdemServicoTests
{
    [Fact]
    public void Dado_CobrancaCriada_Quando_Registrar_Entao_DevePreservarIdentidadeSemConfirmarPagamento()
    {
        // Arrange
        var saga = SagaOrdemServicoTestDataFactory.Criar(EstadoSagaOrdemServico.AguardandoPagamento);
        var pagamentoId = Guid.NewGuid();

        // Act
        saga.RegistrarCobranca(saga.OrcamentoId!.Value, 3, pagamentoId, 150m);
        saga.RegistrarCobranca(saga.OrcamentoId.Value, 3, pagamentoId, 150m);

        // Assert
        saga.PagamentoId.Should().Be(pagamentoId);
        saga.Estado.Should().Be(EstadoSagaOrdemServico.AguardandoPagamento);
    }

    [Fact]
    public void Dado_IdentidadeJaConhecida_Quando_ConfirmarPagamento_Entao_DeveAvancarEstadoSemIgnorarConfirmacao()
    {
        // Arrange
        var saga = SagaOrdemServicoTestDataFactory.Criar(EstadoSagaOrdemServico.AguardandoPagamento);
        var pagamentoId = Guid.NewGuid();
        saga.RegistrarCobranca(saga.OrcamentoId!.Value, 3, pagamentoId, 150m);

        // Act
        saga.RegistrarPagamento(saga.OrcamentoId.Value, 3, pagamentoId, 150m);
        saga.RegistrarPagamento(saga.OrcamentoId.Value, 3, pagamentoId, 150m);
        saga.RegistrarCobranca(saga.OrcamentoId.Value, 3, pagamentoId, 150m);

        // Assert
        saga.Estado.Should().Be(EstadoSagaOrdemServico.PagamentoConfirmado);
        saga.PagamentoId.Should().Be(pagamentoId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dado_OutraCobrancaAssociada_Quando_ReceberResultado_Entao_DeveRejeitarSemSubstituirIdentidade(bool confirmar)
    {
        // Arrange
        var saga = SagaOrdemServicoTestDataFactory.Criar(EstadoSagaOrdemServico.AguardandoPagamento);
        var pagamentoId = Guid.NewGuid();
        saga.RegistrarCobranca(saga.OrcamentoId!.Value, 3, pagamentoId, 150m);

        // Act
        Action tentativa = () =>
        {
            if (confirmar)
            {
                saga.RegistrarPagamento(saga.OrcamentoId.Value, 3, Guid.NewGuid(), 150m);
            }
            else
            {
                saga.RegistrarCobranca(saga.OrcamentoId.Value, 3, Guid.NewGuid(), 150m);
            }
        };

        // Assert
        tentativa.Should().Throw<DomainException>();
        saga.PagamentoId.Should().Be(pagamentoId);
        saga.Estado.Should().Be(EstadoSagaOrdemServico.AguardandoPagamento);
    }

    [Theory]
    [InlineData("estado")]
    [InlineData("identificador")]
    [InlineData("orcamento")]
    [InlineData("versao")]
    [InlineData("valor")]
    public void Dado_CobrancaIncompativel_Quando_Registrar_Entao_DevePreservarSaga(string campo)
    {
        // Arrange
        var estado = campo == "estado" ? EstadoSagaOrdemServico.EmExecucao : EstadoSagaOrdemServico.AguardandoPagamento;
        var saga = SagaOrdemServicoTestDataFactory.Criar(estado);

        // Act
        var tentativa = () => saga.RegistrarCobranca(campo == "orcamento" ? Guid.NewGuid() : saga.OrcamentoId!.Value,
            campo == "versao" ? 2 : 3, campo == "identificador" ? Guid.Empty : Guid.NewGuid(), campo == "valor" ? 149m : 150m);

        // Assert
        tentativa.Should().Throw<DomainException>();
        saga.PagamentoId.Should().BeNull();
        saga.Estado.Should().Be(estado);
    }
}
