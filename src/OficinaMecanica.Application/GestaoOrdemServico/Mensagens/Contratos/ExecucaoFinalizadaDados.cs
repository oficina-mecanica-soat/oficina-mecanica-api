namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;

public sealed record ExecucaoFinalizadaDados(Guid PreparacaoId, int VersaoOrcamento, DateTimeOffset FinalizadoEm);
