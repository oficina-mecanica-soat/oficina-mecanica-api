namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens;

public interface ISnapshotOrcamentoRepository
{
    Task<GerarOrcamentoDados?> ObterAsync(Guid ordemServicoId, int versao, CancellationToken cancellationToken);
}
