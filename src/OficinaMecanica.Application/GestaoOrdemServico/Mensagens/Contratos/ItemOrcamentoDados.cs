namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;

public sealed record ItemOrcamentoDados(
    Guid ItemId,
    string Descricao,
    int Quantidade,
    decimal ValorUnitario);
