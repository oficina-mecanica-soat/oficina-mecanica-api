using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Infrastructure.Persistence;
namespace OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;

public sealed class SagaIntegracaoRepository : ISagaIntegracaoRepository
{
    private readonly OficinaMecanicaDbContext _dbContext;
    public SagaIntegracaoRepository(OficinaMecanicaDbContext dbContext) { _dbContext = dbContext; }
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
