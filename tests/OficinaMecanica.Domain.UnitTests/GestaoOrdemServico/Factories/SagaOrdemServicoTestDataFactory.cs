using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;

namespace OficinaMecanica.Domain.UnitTests.GestaoOrdemServico.Factories;

internal static class SagaOrdemServicoTestDataFactory
{
    public const int VersaoPreparacao = 2;
    public const int VersaoOrcamento = 3;
    public const decimal ValorOrcamento = 150m;
    public static readonly DateTimeOffset FinalizadoEm = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    public static SagaOrdemServico Criar(EstadoSagaOrdemServico estado = EstadoSagaOrdemServico.Preparando)
    {
        var saga = SagaOrdemServico.Iniciar(Guid.NewGuid(), VersaoPreparacao, VersaoOrcamento);
        if (estado == EstadoSagaOrdemServico.Preparando)
            return saga;

        saga.RegistrarPreparacao(Guid.NewGuid(), VersaoPreparacao);
        if (estado == EstadoSagaOrdemServico.AguardandoOrcamento)
            return saga;

        saga.RegistrarOrcamento(Guid.NewGuid(), VersaoOrcamento, ValorOrcamento);
        if (estado == EstadoSagaOrdemServico.AguardandoAprovacao)
            return saga;

        saga.RegistrarDecisao(saga.OrcamentoId!.Value, VersaoOrcamento, DecisaoOrcamento.Aprovado);
        if (estado == EstadoSagaOrdemServico.ExecucaoAutorizada)
            return saga;

        saga.RegistrarInicioFisico(saga.PreparacaoId!.Value, VersaoOrcamento);
        if (estado == EstadoSagaOrdemServico.EmExecucao)
            return saga;

        saga.RegistrarFinalizacao(saga.PreparacaoId.Value, VersaoOrcamento, FinalizadoEm);
        if (estado == EstadoSagaOrdemServico.AguardandoPagamento)
            return saga;

        if (estado != EstadoSagaOrdemServico.PagamentoConfirmado)
            throw new ArgumentOutOfRangeException(nameof(estado));

        saga.RegistrarPagamento(saga.OrcamentoId.Value, VersaoOrcamento, Guid.NewGuid(), ValorOrcamento);
        return saga;
    }
}
