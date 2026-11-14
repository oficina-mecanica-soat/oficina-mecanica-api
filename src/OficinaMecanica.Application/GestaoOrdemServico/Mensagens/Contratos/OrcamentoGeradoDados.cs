namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;

public sealed record OrcamentoGeradoDados(
    Guid OrcamentoId,
    int Versao,
    decimal ValorTotal);
