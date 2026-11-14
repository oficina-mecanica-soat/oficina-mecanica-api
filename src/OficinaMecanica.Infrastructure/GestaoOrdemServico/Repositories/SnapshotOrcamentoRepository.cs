using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Infrastructure.Persistence;

namespace OficinaMecanica.Infrastructure.GestaoOrdemServico.Repositories;

public sealed class SnapshotOrcamentoRepository : ISnapshotOrcamentoRepository
{
    private readonly OficinaMecanicaDbContext _dbContext;

    public SnapshotOrcamentoRepository(OficinaMecanicaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GerarOrcamentoDados?> ObterAsync(Guid ordemServicoId, int versao, CancellationToken cancellationToken)
    {
        var ordem = await _dbContext.OrdensServico.Include(o => o.Servicos).Include(o => o.PecasInsumos)
            .AsSplitQuery().SingleOrDefaultAsync(o => o.Id == ordemServicoId, cancellationToken);
        if (ordem is null)
        {
            return null;
        }

        var clienteId = await _dbContext.Veiculos.Where(v => v.Id == ordem.VeiculoId)
            .Select(v => v.ClienteId).SingleAsync(cancellationToken);
        var servicoIds = ordem.Servicos.Select(s => s.ServicoCatalogoId).Distinct().ToArray();
        var itemIds = ordem.PecasInsumos.Select(i => i.PecaInsumoCatalogoId).Distinct().ToArray();
        var servicos = await _dbContext.ServicosCatalogo.Where(s => servicoIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        var itens = await _dbContext.PecasInsumosCatalogo.Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        if (servicos.Count != servicoIds.Length || itens.Count != itemIds.Length)
        {
            throw new ContratoMensagemException("CATALOGO_REFERENCIADO_NAO_ENCONTRADO");
        }

        // Valores vêm das instâncias da OS; o catálogo próprio fornece somente as descrições.
        return new(versao, clienteId,
            ordem.Servicos.Select(s => new ServicoOrcamentoDados(s.Id, s.ServicoCatalogoId, servicos[s.ServicoCatalogoId].Descricao, s.Valor))
                .OrderBy(s => s.ServicoId).ToArray(),
            ordem.PecasInsumos.Select(i => new ItemOrcamentoDados(i.PecaInsumoCatalogoId, itens[i.PecaInsumoCatalogoId].Descricao, i.Quantidade, i.ValorUnitario))
                .OrderBy(i => i.ItemId).ToArray());
    }
}
