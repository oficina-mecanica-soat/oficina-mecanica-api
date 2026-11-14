namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;

public sealed record OrcamentoDecididoDados(
    Guid OrcamentoId,
    int Versao,
    string Decisao);
