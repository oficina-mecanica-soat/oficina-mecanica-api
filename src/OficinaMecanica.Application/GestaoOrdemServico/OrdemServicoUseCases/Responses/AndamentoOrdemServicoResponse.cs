namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.Responses;

public sealed record AndamentoOrdemServicoResponse(
    Guid OrdemServicoId,
    Guid SagaId,
    string Estado,
    Guid? PreparacaoId,
    Guid? OrcamentoId,
    int VersaoOrcamento,
    decimal? ValorOrcamento,
    Guid? PagamentoId,
    bool PagamentoConfirmado,
    DateTimeOffset? FinalizadoEm);
