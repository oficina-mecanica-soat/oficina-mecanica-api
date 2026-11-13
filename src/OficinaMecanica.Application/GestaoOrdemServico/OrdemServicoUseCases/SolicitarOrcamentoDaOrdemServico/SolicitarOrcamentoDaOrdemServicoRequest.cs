namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoDaOrdemServico;

public sealed record SolicitarOrcamentoDaOrdemServicoRequest(Guid SagaId, Guid OrdemServicoId, Guid OperacaoId, string CorrelationId);
