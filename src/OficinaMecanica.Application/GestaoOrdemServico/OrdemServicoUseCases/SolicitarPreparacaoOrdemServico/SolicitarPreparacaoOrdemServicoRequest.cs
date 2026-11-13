namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarPreparacaoOrdemServico;

public sealed record SolicitarPreparacaoOrdemServicoRequest(Guid OrdemServicoId, Guid FilialId, Guid OperacaoId, string CorrelationId);
