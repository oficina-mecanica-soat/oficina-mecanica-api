namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;

public sealed record PrepararExecucaoDados(
    int Versao,
    Guid FilialId,
    Guid VeiculoId,
    Guid MecanicoId,
    ItemPreparacaoDados[] Itens,
    ServicoOrcamentoDados[] Servicos);
