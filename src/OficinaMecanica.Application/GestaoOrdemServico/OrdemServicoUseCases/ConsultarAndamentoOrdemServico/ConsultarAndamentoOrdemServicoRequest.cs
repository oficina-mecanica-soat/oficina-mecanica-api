namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ConsultarAndamentoOrdemServico;

public sealed record ConsultarAndamentoOrdemServicoRequest(Guid OrdemServicoId, Guid? ClienteId = null);
