namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;

public sealed record ExecucaoPreparadaDados(
    Guid PreparacaoId,
    int Versao,
    ItemPreparacaoDados[] ItensReservados,
    DiagnosticoPreparacaoDados Diagnostico);
