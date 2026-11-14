using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Infrastructure.Persistence;

namespace OficinaMecanica.Infrastructure.GestaoOrdemServico.Repositories;

public sealed class SagaIntegracaoRepository : ISagaIntegracaoRepository
{
    private readonly OficinaMecanicaDbContext _dbContext;

    public SagaIntegracaoRepository(OficinaMecanicaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<SagaOrdemServico?> ObterPorOrdemAsync(Guid ordemServicoId, CancellationToken cancellationToken) =>
        _dbContext.SagasOrdemServico.SingleOrDefaultAsync(s => s.OrdemServicoId == ordemServicoId, cancellationToken);

    public async Task AdicionarAsync(SagaOrdemServico saga, GerarOrcamentoDados snapshot, CancellationToken cancellationToken)
    {
        await _dbContext.SagasOrdemServico.AddAsync(saga, cancellationToken);
        _dbContext.Entry(saga).Property<string?>("SnapshotOrcamento").CurrentValue = JsonContrato.Serializar(snapshot);
    }

    public Task<GerarOrcamentoDados?> ObterSnapshotAsync(SagaOrdemServico saga, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = _dbContext.Entry(saga).Property<string?>("SnapshotOrcamento").CurrentValue;
        return Task.FromResult(json is null ? null : JsonContrato.Ler<GerarOrcamentoDados>(json));
    }

    public Task<SagaOrdemServico?> ObterAsync(Guid sagaId, Guid ordemServicoId, CancellationToken cancellationToken) =>
        _dbContext.SagasOrdemServico.SingleOrDefaultAsync(s => s.Id == sagaId && s.OrdemServicoId == ordemServicoId, cancellationToken);
    public async Task<MensagemIntegracao?> ObterComandoAsync(string tipo, Guid ordemServicoId, int versao, CancellationToken cancellationToken)
    {
        var comando = _dbContext.Set<RegistroOutboxSaga>().Local.SingleOrDefault(r => r.Tipo == tipo && r.OrdemServicoId == ordemServicoId && r.VersaoNegocio == versao)
            ?? await _dbContext.Set<RegistroOutboxSaga>().SingleOrDefaultAsync(r => r.Tipo == tipo && r.OrdemServicoId == ordemServicoId && r.VersaoNegocio == versao, cancellationToken);
        return comando is null ? null : JsonContrato.LerEnvelope(comando.Corpo);
    }

    public async Task AdicionarComandoAsync(MensagemIntegracao mensagem, int versao, string destino, CancellationToken cancellationToken) =>
        await _dbContext.AddAsync(RegistroOutboxSaga.Criar(mensagem, versao, destino), cancellationToken);
}
