using System.Data;
using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Infrastructure.Persistence;

namespace OficinaMecanica.Infrastructure.GestaoOrdemServico.Repositories;

public sealed class RecebimentoMensagemRepository : IRecebimentoMensagemRepository
{
    private readonly OficinaMecanicaDbContext _dbContext;

    public RecebimentoMensagemRepository(OficinaMecanicaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ExecutarEmTransacaoAsync(Func<CancellationToken, Task> operacao, CancellationToken cancellationToken)
    {
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            await operacao(cancellationToken);
            return;
        }
        await using var transacao = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await operacao(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
    }

    public async Task<bool> VerificarRepeticaoAsync(Guid mensagemId, string chave, string hash, CancellationToken cancellationToken)
    {
        var recebimentos = _dbContext.Set<RecebimentoMensagem>();
        var recebido = await recebimentos.FindAsync([mensagemId], cancellationToken);
        if (recebido is not null)
        {
            if (recebido.ChaveOperacao != chave || recebido.Hash != hash)
            {
                throw new ContratoMensagemException("MENSAGEM_DIVERGENTE");
            }
            return true;
        }
        var operacao = await recebimentos.FirstOrDefaultAsync(r => r.ChaveOperacao == chave, cancellationToken);
        if (operacao is null)
        {
            return false;
        }
        if (operacao.Hash != hash)
        {
            throw new ContratoMensagemException("OPERACAO_DIVERGENTE");
        }
        return true;
    }

    public async Task RegistrarAsync(Guid mensagemId, string chave, string hash, CancellationToken cancellationToken)
    {
        if (await _dbContext.Set<RecebimentoMensagem>().FindAsync([mensagemId], cancellationToken) is null)
        {
            await _dbContext.AddAsync(new RecebimentoMensagem { Id = mensagemId, ChaveOperacao = chave, Hash = hash }, cancellationToken);
        }
    }
}
