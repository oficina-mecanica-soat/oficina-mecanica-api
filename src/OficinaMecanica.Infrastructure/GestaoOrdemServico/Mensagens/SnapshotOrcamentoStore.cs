using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Infrastructure.Persistence;

namespace OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;

public sealed class SnapshotOrcamentoStore : ISnapshotOrcamentoStore
{
    private readonly OficinaMecanicaDbContext _dbContext;

    public SnapshotOrcamentoStore(OficinaMecanicaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RegistrarAsync(Guid sagaId, GerarOrcamentoDados snapshot, CancellationToken cancellationToken)
    {
        var saga = await _dbContext.SagasOrdemServico.FindAsync([sagaId], cancellationToken)
            ?? throw new ContratoMensagemException("SAGA_NAO_ENCONTRADA");
        _dbContext.Entry(saga).Property<string?>("SnapshotOrcamento").CurrentValue = JsonContrato.Serializar(snapshot);
    }

    public async Task<GerarOrcamentoDados?> ObterAsync(Guid sagaId, CancellationToken cancellationToken)
    {
        var saga = await _dbContext.SagasOrdemServico.FindAsync([sagaId], cancellationToken);
        if (saga is null)
        {
            return null;
        }

        var json = _dbContext.Entry(saga).Property<string?>("SnapshotOrcamento").CurrentValue;
        return json is null ? null : JsonContrato.Ler<GerarOrcamentoDados>(json);
    }


}
