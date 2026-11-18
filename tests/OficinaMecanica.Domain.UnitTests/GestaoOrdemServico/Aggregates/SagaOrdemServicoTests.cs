using FluentAssertions;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Domain.GestaoOrdemServico.Messages;
using OficinaMecanica.Domain.Shared.Exceptions;
using OficinaMecanica.Domain.UnitTests.GestaoOrdemServico.Factories;
using Dados = OficinaMecanica.Domain.UnitTests.GestaoOrdemServico.Factories.SagaOrdemServicoTestDataFactory;

namespace OficinaMecanica.Domain.UnitTests.GestaoOrdemServico.Aggregates;

public class SagaOrdemServicoTests
{
    [Fact]
    public void Dado_OrdemValida_Quando_IniciarSaga_Entao_DevePreservarIdentidadeEVersoesIndependentes()
    {
        // Arrange
        var ordemId = Guid.NewGuid();

        // Act
        var saga = SagaOrdemServico.Iniciar(ordemId, Dados.VersaoPreparacao, Dados.VersaoOrcamento);

        // Assert
        saga.Id.Should().NotBeEmpty();
        saga.OrdemServicoId.Should().Be(ordemId);
        saga.VersaoPreparacao.Should().Be(2);
        saga.VersaoOrcamento.Should().Be(3);
        saga.Estado.Should().Be(EstadoSagaOrdemServico.Preparando);
        saga.PreparacaoId.Should().BeNull();
        saga.OrcamentoId.Should().BeNull();
        saga.ValorOrcamento.Should().BeNull();
        saga.Decisao.Should().BeNull();
        saga.InicioFisicoConfirmado.Should().BeFalse();
        saga.PreparacaoCancelada.Should().BeFalse();
        saga.OrcamentoCancelado.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, 1, 1)]
    [InlineData(false, 0, 1)]
    [InlineData(false, -1, 1)]
    [InlineData(false, 1, 0)]
    [InlineData(false, 1, -1)]
    public void Dado_IdentidadeOuVersaoInvalida_Quando_Iniciar_Entao_DeveRejeitar(bool idVazio, int preparacao, int orcamento)
    {
        // Arrange
        var ordemId = idVazio ? Guid.Empty : Guid.NewGuid();

        // Act
        var acao = () => SagaOrdemServico.Iniciar(ordemId, preparacao, orcamento);

        // Assert
        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void Dado_FluxoAprovado_Quando_FinalizarExecucao_Entao_DeveAguardarPagamentoSeparadamente()
    {
        // Arrange
        var saga = Dados.Criar(EstadoSagaOrdemServico.ExecucaoAutorizada);
        saga.InicioFisicoConfirmado.Should().BeFalse();

        // Act
        saga.RegistrarInicioFisico(saga.PreparacaoId!.Value, Dados.VersaoOrcamento);
        saga.RegistrarFinalizacao(saga.PreparacaoId.Value, Dados.VersaoOrcamento, Dados.FinalizadoEm);

        // Assert
        saga.Estado.Should().Be(EstadoSagaOrdemServico.AguardandoPagamento);
        saga.PagamentoId.Should().BeNull();
        saga.FinalizadoEm.Should().Be(Dados.FinalizadoEm);
        saga.InicioFisicoConfirmado.Should().BeTrue();
        ((int)StatusOrdemServico.Finalizada).Should().Be(5);
    }

    [Fact]
    public void Dado_ExecucaoFinalizada_Quando_ConfirmarPagamentoCompativel_Entao_DeveLiberarPagamentoUmaVez()
    {
        // Arrange
        var saga = Dados.Criar(EstadoSagaOrdemServico.AguardandoPagamento);
        var pagamentoId = Guid.NewGuid();

        // Act
        saga.RegistrarPagamento(saga.OrcamentoId!.Value, Dados.VersaoOrcamento, pagamentoId, Dados.ValorOrcamento);
        saga.RegistrarPagamento(saga.OrcamentoId.Value, Dados.VersaoOrcamento, pagamentoId, Dados.ValorOrcamento);

        // Assert
        saga.Estado.Should().Be(EstadoSagaOrdemServico.PagamentoConfirmado);
        saga.PagamentoId.Should().Be(pagamentoId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dado_OrcamentoRecusado_Quando_ConfirmarCompensacoesEmQualquerOrdem_Entao_DeveAguardarAmbas(bool orcamentoPrimeiro)
    {
        // Arrange
        var saga = Dados.Criar(EstadoSagaOrdemServico.AguardandoAprovacao);
        saga.RegistrarDecisao(saga.OrcamentoId!.Value, Dados.VersaoOrcamento, DecisaoOrcamento.Recusado);

        // Act
        if (orcamentoPrimeiro)
            saga.ConfirmarCancelamentoOrcamento(Dados.VersaoOrcamento);
        else
            saga.ConfirmarCancelamentoPreparacao(saga.PreparacaoId!.Value, Dados.VersaoPreparacao);

        // Assert
        saga.Estado.Should().Be(EstadoSagaOrdemServico.CompensacaoPendente);

        // Act
        saga.ConfirmarCancelamentoPreparacao(saga.PreparacaoId!.Value, Dados.VersaoPreparacao);
        saga.ConfirmarCancelamentoOrcamento(Dados.VersaoOrcamento);
        saga.ConfirmarCancelamentoPreparacao(saga.PreparacaoId.Value, Dados.VersaoPreparacao);
        saga.ConfirmarCancelamentoOrcamento(Dados.VersaoOrcamento);
        saga.IniciarCompensacao();

        // Assert
        saga.Estado.Should().Be(EstadoSagaOrdemServico.Compensada);
        saga.PreparacaoCancelada.Should().BeTrue();
        saga.OrcamentoCancelado.Should().BeTrue();
    }

    [Fact]
    public void Dado_FalhaAoGerarOrcamento_Quando_ConfirmarCancelamentoPorVersao_Entao_DeveCompensarSemOrcamentoCriado()
    {
        // Arrange
        var saga = Dados.Criar(EstadoSagaOrdemServico.AguardandoOrcamento);

        // Act
        saga.IniciarCompensacao();
        saga.IniciarCompensacao();
        saga.ConfirmarCancelamentoOrcamento(Dados.VersaoOrcamento);
        saga.ConfirmarCancelamentoPreparacao(saga.PreparacaoId!.Value, Dados.VersaoPreparacao);

        // Assert
        saga.Estado.Should().Be(EstadoSagaOrdemServico.Compensada);
        saga.OrcamentoId.Should().BeNull();
    }

    [Theory]
    [InlineData(EstadoSagaOrdemServico.AguardandoOrcamento)]
    [InlineData(EstadoSagaOrdemServico.AguardandoAprovacao)]
    [InlineData(EstadoSagaOrdemServico.ExecucaoAutorizada)]
    public void Dado_FalhaAntesDoInicioFisico_Quando_IniciarCompensacao_Entao_DeveAguardarConfirmacoes(EstadoSagaOrdemServico estado)
    {
        // Arrange
        var saga = Dados.Criar(estado);

        // Act
        saga.IniciarCompensacao();

        // Assert
        saga.Estado.Should().Be(EstadoSagaOrdemServico.CompensacaoPendente);
        saga.PreparacaoCancelada.Should().BeFalse();
        saga.OrcamentoCancelado.Should().BeFalse();
    }

    [Fact]
    public void Dado_InicioVencedorMasEventoTardio_Quando_ReceberDuranteCompensacao_Entao_DeveExigirRecuperacaoSemEstornoFicticio()
    {
        // Arrange
        var saga = Dados.Criar(EstadoSagaOrdemServico.ExecucaoAutorizada);
        saga.IniciarCompensacao();

        // Act
        saga.RegistrarInicioFisico(saga.PreparacaoId!.Value, Dados.VersaoOrcamento);
        var cancelar = () => saga.ConfirmarCancelamentoPreparacao(saga.PreparacaoId.Value, Dados.VersaoPreparacao);

        // Assert
        saga.Estado.Should().Be(EstadoSagaOrdemServico.RecuperacaoPendente);
        saga.InicioFisicoConfirmado.Should().BeTrue();
        saga.PreparacaoCancelada.Should().BeFalse();
        cancelar.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(EstadoSagaOrdemServico.EmExecucao)]
    [InlineData(EstadoSagaOrdemServico.AguardandoPagamento)]
    [InlineData(EstadoSagaOrdemServico.PagamentoConfirmado)]
    public void Dado_InicioFisicoConfirmado_Quando_Compensar_Entao_DevePreservarEstado(EstadoSagaOrdemServico estado)
    {
        // Arrange
        var saga = Dados.Criar(estado);

        // Act
        var acao = () => saga.IniciarCompensacao();

        // Assert
        acao.Should().Throw<DomainException>().WithMessage(SagaOrdemServicoErrorMessages.CompensacaoAposInicioFisico);
        saga.Estado.Should().Be(estado);
    }

    [Fact]
    public void Dado_SagaConcluida_Quando_RepetirResultados_Entao_NaoDeveRegredirOuAlterarIdentidades()
    {
        // Arrange
        var saga = Dados.Criar(EstadoSagaOrdemServico.PagamentoConfirmado);

        // Act
        saga.RegistrarPreparacao(saga.PreparacaoId!.Value, Dados.VersaoPreparacao);
        saga.RegistrarOrcamento(saga.OrcamentoId!.Value, Dados.VersaoOrcamento, Dados.ValorOrcamento);
        saga.RegistrarDecisao(saga.OrcamentoId.Value, Dados.VersaoOrcamento, DecisaoOrcamento.Aprovado);
        saga.RegistrarInicioFisico(saga.PreparacaoId.Value, Dados.VersaoOrcamento);
        saga.RegistrarFinalizacao(saga.PreparacaoId.Value, Dados.VersaoOrcamento, Dados.FinalizadoEm);

        // Assert
        saga.Estado.Should().Be(EstadoSagaOrdemServico.PagamentoConfirmado);
        saga.FinalizadoEm.Should().Be(Dados.FinalizadoEm);
    }

    [Fact]
    public void Dado_SagaCompensada_Quando_RepetirPreparacaoERecusa_Entao_NaoDeveReabrirExecucao()
    {
        // Arrange
        var saga = Dados.Criar(EstadoSagaOrdemServico.AguardandoAprovacao);
        saga.RegistrarDecisao(saga.OrcamentoId!.Value, Dados.VersaoOrcamento, DecisaoOrcamento.Recusado);
        saga.ConfirmarCancelamentoPreparacao(saga.PreparacaoId!.Value, Dados.VersaoPreparacao);
        saga.ConfirmarCancelamentoOrcamento(Dados.VersaoOrcamento);

        // Act
        saga.RegistrarPreparacao(saga.PreparacaoId.Value, Dados.VersaoPreparacao);
        saga.RegistrarDecisao(saga.OrcamentoId.Value, Dados.VersaoOrcamento, DecisaoOrcamento.Recusado);
        var iniciar = () => saga.RegistrarInicioFisico(saga.PreparacaoId.Value, Dados.VersaoOrcamento);

        // Assert
        iniciar.Should().Throw<DomainException>();
        saga.Estado.Should().Be(EstadoSagaOrdemServico.Compensada);
    }

    [Fact]
    public void Dado_PreparacaoRecusada_Quando_RepetirResultado_Entao_NaoDeveCriarReservaOuCompensacao()
    {
        // Arrange
        var saga = Dados.Criar();

        // Act
        saga.RegistrarRecusaPreparacao(Dados.VersaoPreparacao);
        saga.RegistrarRecusaPreparacao(Dados.VersaoPreparacao);
        var preparar = () => saga.RegistrarPreparacao(Guid.NewGuid(), Dados.VersaoPreparacao);
        var compensar = () => saga.IniciarCompensacao();

        // Assert
        saga.Estado.Should().Be(EstadoSagaOrdemServico.PreparacaoRecusada);
        saga.PreparacaoId.Should().BeNull();
        preparar.Should().Throw<DomainException>();
        compensar.Should().Throw<DomainException>();
    }

    [Fact]
    public void Dado_CompensacaoPendente_Quando_ChegarOrcamentoTardio_Entao_DevePreservarCompensacao()
    {
        // Arrange
        var saga = Dados.Criar(EstadoSagaOrdemServico.AguardandoOrcamento);
        saga.IniciarCompensacao();

        // Act
        saga.RegistrarOrcamento(Guid.NewGuid(), Dados.VersaoOrcamento, Dados.ValorOrcamento);

        // Assert
        saga.Estado.Should().Be(EstadoSagaOrdemServico.CompensacaoPendente);
        saga.OrcamentoId.Should().NotBeNull();
    }

    [Fact]
    public void Dado_ResultadosIncompativeis_Quando_Registrar_Entao_DeveRejeitarSemAlterarSaga()
    {
        // Arrange
        var saga = Dados.Criar(EstadoSagaOrdemServico.AguardandoAprovacao);
        Action[] resultados =
        [
            () => saga.RegistrarPreparacao(Guid.NewGuid(), Dados.VersaoPreparacao),
            () => saga.RegistrarPreparacao(saga.PreparacaoId!.Value, 1),
            () => saga.RegistrarRecusaPreparacao(1),
            () => saga.RegistrarOrcamento(saga.OrcamentoId!.Value, 1, Dados.ValorOrcamento),
            () => saga.RegistrarOrcamento(Guid.NewGuid(), Dados.VersaoOrcamento, Dados.ValorOrcamento),
            () => saga.RegistrarDecisao(Guid.NewGuid(), Dados.VersaoOrcamento, DecisaoOrcamento.Aprovado),
            () => saga.RegistrarDecisao(saga.OrcamentoId!.Value, 1, DecisaoOrcamento.Aprovado),
            () => saga.RegistrarDecisao(saga.OrcamentoId!.Value, Dados.VersaoOrcamento, (DecisaoOrcamento)99),
            () => saga.RegistrarInicioFisico(Guid.NewGuid(), Dados.VersaoOrcamento),
            () => saga.RegistrarFinalizacao(saga.PreparacaoId!.Value, Dados.VersaoOrcamento, Dados.FinalizadoEm),
            () => saga.RegistrarPagamento(saga.OrcamentoId!.Value, Dados.VersaoOrcamento, Guid.NewGuid(), Dados.ValorOrcamento),
            () => saga.ConfirmarCancelamentoPreparacao(saga.PreparacaoId!.Value, Dados.VersaoPreparacao),
            () => saga.ConfirmarCancelamentoOrcamento(Dados.VersaoOrcamento)
        ];

        // Act / Assert
        foreach (var resultado in resultados)
        {
            resultado.Should().Throw<DomainException>();
            saga.Estado.Should().Be(EstadoSagaOrdemServico.AguardandoAprovacao);
            saga.Decisao.Should().BeNull();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(10000000000000000)]
    public void Dado_ValorOrcamentoInvalido_Quando_Registrar_Entao_DeveRejeitar(decimal valor)
    {
        // Arrange
        var saga = Dados.Criar(EstadoSagaOrdemServico.AguardandoOrcamento);

        // Act
        var acao = () => saga.RegistrarOrcamento(Guid.NewGuid(), Dados.VersaoOrcamento, valor);

        // Assert
        acao.Should().Throw<DomainException>().WithMessage(SagaOrdemServicoErrorMessages.ValorInvalido);
        saga.OrcamentoId.Should().BeNull();
    }

    [Fact]
    public void Dado_ExecucaoIniciada_Quando_FinalizacaoSemUtcOuPagamentoDivergente_Entao_DevePreservarEstado()
    {
        // Arrange
        var saga = Dados.Criar(EstadoSagaOrdemServico.EmExecucao);
        Action[] datasInvalidas =
        [
            () => saga.RegistrarFinalizacao(saga.PreparacaoId!.Value, Dados.VersaoOrcamento, default),
            () => saga.RegistrarFinalizacao(saga.PreparacaoId!.Value, Dados.VersaoOrcamento, Dados.FinalizadoEm.ToOffset(TimeSpan.FromHours(-3)))
        ];

        // Act / Assert
        foreach (var acao in datasInvalidas)
            acao.Should().Throw<DomainException>();

        saga.RegistrarFinalizacao(saga.PreparacaoId!.Value, Dados.VersaoOrcamento, Dados.FinalizadoEm);
        var pagar = () => saga.RegistrarPagamento(saga.OrcamentoId!.Value, Dados.VersaoOrcamento, Guid.NewGuid(), Dados.ValorOrcamento - 1);
        pagar.Should().Throw<DomainException>();
        saga.Estado.Should().Be(EstadoSagaOrdemServico.AguardandoPagamento);
        saga.PagamentoId.Should().BeNull();
    }

    [Fact]
    public void Dado_CompensacaoPendente_Quando_ConfirmacaoDivergente_Entao_NaoDeveConcluirCompensacao()
    {
        // Arrange
        var saga = Dados.Criar(EstadoSagaOrdemServico.AguardandoOrcamento);
        saga.IniciarCompensacao();
        Action[] resultados =
        [
            () => saga.ConfirmarCancelamentoPreparacao(saga.PreparacaoId!.Value, 1),
            () => saga.ConfirmarCancelamentoPreparacao(Guid.Empty, Dados.VersaoPreparacao),
            () => saga.ConfirmarCancelamentoOrcamento(1)
        ];

        // Act / Assert
        foreach (var resultado in resultados)
            resultado.Should().Throw<DomainException>();

        saga.PreparacaoCancelada.Should().BeFalse();
        saga.OrcamentoCancelado.Should().BeFalse();
        saga.Estado.Should().Be(EstadoSagaOrdemServico.CompensacaoPendente);
    }

    [Fact]
    public void Dado_ValoresCompativeisComSql_Quando_RegistrarOrcamento_Entao_NaoDeveArredondarOuTruncar()
    {
        // Arrange
        var saga = Dados.Criar(EstadoSagaOrdemServico.AguardandoOrcamento);
        const decimal valor = 9999999999999999.99m;

        // Act
        saga.RegistrarOrcamento(Guid.NewGuid(), Dados.VersaoOrcamento, valor);

        // Assert
        saga.ValorOrcamento.Should().Be(valor);
    }

    [Fact]
    public void Dado_SagaSemPreparacao_Quando_ReceberOrcamento_Entao_DevePreservarOrdemDasEtapas()
    {
        // Arrange
        var saga = Dados.Criar();

        // Act
        var orcar = () => saga.RegistrarOrcamento(Guid.NewGuid(), Dados.VersaoOrcamento, Dados.ValorOrcamento);
        var prepararSemId = () => saga.RegistrarPreparacao(Guid.Empty, Dados.VersaoPreparacao);
        var orcarSemId = () => saga.RegistrarOrcamento(Guid.Empty, Dados.VersaoOrcamento, Dados.ValorOrcamento);

        // Assert
        orcar.Should().Throw<DomainException>();
        prepararSemId.Should().Throw<DomainException>();
        orcarSemId.Should().Throw<DomainException>();
        saga.Estado.Should().Be(EstadoSagaOrdemServico.Preparando);
    }

    [Fact]
    public void Dado_OrcamentoPendenteDeCompensacao_Quando_ReceberOutroResultado_Entao_NaoDeveSobrescreverSnapshot()
    {
        // Arrange
        var saga = Dados.Criar(EstadoSagaOrdemServico.AguardandoAprovacao);
        var orcamentoId = saga.OrcamentoId;
        saga.IniciarCompensacao();

        // Act
        var substituir = () => saga.RegistrarOrcamento(Guid.NewGuid(), Dados.VersaoOrcamento, Dados.ValorOrcamento);

        // Assert
        substituir.Should().Throw<DomainException>().WithMessage(SagaOrdemServicoErrorMessages.ResultadoDivergente);
        saga.OrcamentoId.Should().Be(orcamentoId);
    }
}
