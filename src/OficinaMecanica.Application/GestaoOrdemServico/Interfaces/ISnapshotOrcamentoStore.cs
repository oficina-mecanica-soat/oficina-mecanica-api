using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;

namespace OficinaMecanica.Application.GestaoOrdemServico.Interfaces;

public interface ISnapshotOrcamentoStore
{
    Task RegistrarAsync(Guid sagaId, GerarOrcamentoDados snapshot, CancellationToken cancellationToken);
    Task<GerarOrcamentoDados?> ObterAsync(Guid sagaId, CancellationToken cancellationToken);
}
