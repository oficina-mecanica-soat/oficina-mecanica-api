namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;

public sealed record OrcamentoCanceladoDados(
    Guid? OrcamentoId,
    int Versao);
