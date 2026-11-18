using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Infrastructure.Persistence;

namespace OficinaMecanica.Infrastructure.GestaoOrdemServico.Repositories;

public sealed class SagaOrdemServicoRepository : ISagaOrdemServicoRepository
{
    private readonly OficinaMecanicaDbContext _dbContext;

    public SagaOrdemServicoRepository(OficinaMecanicaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AdicionarAsync(SagaOrdemServico saga, CancellationToken cancellationToken = default)
    {
        await _dbContext.SagasOrdemServico.AddAsync(saga, cancellationToken);
    }

    public Task<SagaOrdemServico?> ObterAsync(Guid sagaId, Guid ordemServicoId, CancellationToken cancellationToken = default)
    {
        return _dbContext.SagasOrdemServico.SingleOrDefaultAsync(saga => saga.Id == sagaId && saga.OrdemServicoId == ordemServicoId, cancellationToken);
    }

    public Task<SagaOrdemServico?> ObterPorOrdemAsync(Guid ordemServicoId, CancellationToken cancellationToken = default)
    {
        return _dbContext.SagasOrdemServico.SingleOrDefaultAsync(saga => saga.OrdemServicoId == ordemServicoId, cancellationToken);
    }
}
