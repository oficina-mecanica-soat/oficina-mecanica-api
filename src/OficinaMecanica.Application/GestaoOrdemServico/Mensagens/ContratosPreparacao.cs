namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens;

public sealed record PrepararExecucaoDados(int Versao, Guid FilialId, Guid VeiculoId, Guid MecanicoId,
    ItemPreparacaoDados[] Itens, ServicoOrcamentoDados[] Servicos);
public sealed record ItemPreparacaoDados(Guid ItemId, int Quantidade);
public sealed record DiagnosticoPreparacaoDados(Guid VeiculoId, Guid MecanicoId, ServicoOrcamentoDados[] Servicos);
public sealed record ExecucaoPreparadaDados(Guid PreparacaoId, int Versao, ItemPreparacaoDados[] ItensReservados, DiagnosticoPreparacaoDados Diagnostico);
public sealed record PreparacaoRecusadaDados(int Versao, string CodigoMotivo);
