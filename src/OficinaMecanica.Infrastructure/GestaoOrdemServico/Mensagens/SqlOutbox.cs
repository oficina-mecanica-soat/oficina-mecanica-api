using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Infrastructure.Observabilidade;
using OficinaMecanica.Infrastructure.Persistence;

namespace OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;

public sealed class SqlOutbox : IOutbox
{
    private readonly OficinaMecanicaDbContext _dbContext;
    private readonly ObservabilidadeMensagens? _observabilidade;

    public SqlOutbox(OficinaMecanicaDbContext dbContext, ObservabilidadeMensagens? observabilidade = null)
    {
        _dbContext = dbContext;
        _observabilidade = observabilidade;
    }

    public async Task<MensagemIntegracao?> ObterComandoAsync(string tipo, Guid ordemServicoId, int versao, CancellationToken cancellationToken)
    {
        var comando = _dbContext.Set<RegistroOutboxSaga>().Local.SingleOrDefault(registro => registro.Tipo == tipo && registro.OrdemServicoId == ordemServicoId && registro.VersaoNegocio == versao)
            ?? await _dbContext.Set<RegistroOutboxSaga>().SingleOrDefaultAsync(registro => registro.Tipo == tipo && registro.OrdemServicoId == ordemServicoId && registro.VersaoNegocio == versao, cancellationToken);

        return comando is null ? null : JsonContrato.LerEnvelope(comando.Corpo);
    }

    public async Task AdicionarComandoAsync(MensagemIntegracao mensagem, int versao, string destino, CancellationToken cancellationToken)
    {
        if (_observabilidade is not null)
        {
            mensagem = mensagem with { TraceContext = _observabilidade.CapturarContexto(mensagem.TraceContext) };
        }
        await _dbContext.AddAsync(RegistroOutboxSaga.Criar(mensagem, versao, destino), cancellationToken);
    }

}
