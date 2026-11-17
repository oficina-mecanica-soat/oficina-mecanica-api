namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ConsultarStatusOrdemServico;

public sealed record ConsultarStatusOrdemServicoResponse(
    Guid OrdemServicoId,
    int Numero,
    string Status,
    IReadOnlyCollection<ServicoStatusResponse> Servicos,
    bool ExecucaoDistribuida = false,
    Guid? PreparacaoId = null);

public sealed record ServicoStatusResponse(Guid ServicoId, Guid ServicoCatalogoId, string Status);
