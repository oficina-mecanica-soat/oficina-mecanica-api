namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;

public sealed record DiagnosticoPreparacaoDados(
    Guid VeiculoId,
    Guid MecanicoId,
    ServicoOrcamentoDados[] Servicos);
