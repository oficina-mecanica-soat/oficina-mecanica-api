using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;

namespace OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;

public interface ISagaOrdemServicoRepository
{
    Task AdicionarAsync(SagaOrdemServico saga, CancellationToken cancellationToken = default);
    Task<SagaOrdemServico?> ObterAsync(Guid sagaId, Guid ordemServicoId, CancellationToken cancellationToken = default);
    Task<SagaOrdemServico?> ObterPorOrdemAsync(Guid ordemServicoId, CancellationToken cancellationToken = default);
}
