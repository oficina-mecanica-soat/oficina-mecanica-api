using System.Text.Json;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens;

public sealed class ProcessadorResultadoOrcamento
{
    private readonly IRecebimentoMensagemRepository _recebimentos;
    private readonly ISagaIntegracaoRepository _sagas;
    private readonly TimeProvider _relogio;
    public ProcessadorResultadoOrcamento(IRecebimentoMensagemRepository recebimentos, ISagaIntegracaoRepository sagas, TimeProvider relogio)
    { _recebimentos = recebimentos; _sagas = sagas; _relogio = relogio; }
    public async Task ProcessarAsync(string json, CancellationToken cancellationToken = default)
    {
        var mensagem = JsonContrato.LerEnvelope(json);
        object dados;
        int versao;
        switch (mensagem.Tipo)
        {
            case "PreparacaoCancelada":
                var preparacao = JsonContrato.Ler<PreparacaoCanceladaDados>(mensagem.Dados.GetRawText());
                if (preparacao.PreparacaoId == Guid.Empty) { throw new ContratoMensagemException("PREPARACAO_INVALIDA"); }
                dados = preparacao; versao = preparacao.Versao; break;
            case "OrcamentoGerado":
                var gerado = JsonContrato.Ler<OrcamentoGeradoDados>(mensagem.Dados.GetRawText());
                if (gerado.OrcamentoId == Guid.Empty) { throw new ContratoMensagemException("ORCAMENTO_INVALIDO"); }
                dados = gerado; versao = gerado.Versao; break;
            case "OrcamentoDecidido":
                var decidido = JsonContrato.Ler<OrcamentoDecididoDados>(mensagem.Dados.GetRawText());
                if (decidido.OrcamentoId == Guid.Empty || decidido.Decisao is not ("Aprovado" or "Recusado")) { throw new ContratoMensagemException("DECISAO_INVALIDA"); }
                dados = decidido; versao = decidido.Versao; break;
            case "OrcamentoCancelado":
                var cancelado = JsonContrato.Ler<OrcamentoCanceladoDados>(mensagem.Dados.GetRawText());
                if (cancelado.OrcamentoId == Guid.Empty) { throw new ContratoMensagemException("ORCAMENTO_INVALIDO"); }
                dados = cancelado; versao = cancelado.Versao; break;
            default: throw new ContratoMensagemException("TIPO_DESCONHECIDO");
        }
        if (versao <= 0) { throw new ContratoMensagemException("VERSAO_INVALIDA"); }
        var chave = $"{mensagem.Tipo}:{mensagem.OrdemServicoId:D}:{versao}";
        var hash = JsonContrato.Hash(mensagem, dados);
        await _recebimentos.ExecutarEmTransacaoAsync(async ct =>
        {
            if (!await _recebimentos.VerificarRepeticaoAsync(mensagem.MensagemId, chave, hash, ct))
            {
                var saga = await _sagas.ObterAsync(mensagem.SagaId, mensagem.OrdemServicoId, ct)
                    ?? throw new ContratoMensagemException("SAGA_NAO_ENCONTRADA");
                if (dados is OrcamentoGeradoDados gerado)
                {
                    var comando = await _sagas.ObterComandoAsync("GerarOrcamento", saga.OrdemServicoId, versao, ct)
                        ?? throw new ContratoMensagemException("GERACAO_NAO_SOLICITADA");
                    if (mensagem.CausationId != comando.MensagemId) { throw new ContratoMensagemException("CAUSACAO_DIVERGENTE"); }
                    var solicitado = JsonContrato.Ler<GerarOrcamentoDados>(comando.Dados.GetRawText());
                    var total = solicitado.Servicos.Sum(s => s.Valor) + solicitado.Itens.Sum(i => i.Quantidade * i.ValorUnitario);
                    if (gerado.ValorTotal != total || mensagem.CorrelationId != comando.CorrelationId)
                    { throw new ContratoMensagemException("SNAPSHOT_DIVERGENTE"); }
                    saga.RegistrarOrcamento(gerado.OrcamentoId, versao, gerado.ValorTotal);
                }
                else if (dados is OrcamentoDecididoDados decidido)
                {
                    saga.RegistrarDecisao(decidido.OrcamentoId, versao, Enum.Parse<DecisaoOrcamento>(decidido.Decisao));
                    if (saga.Estado == EstadoSagaOrdemServico.ExecucaoAutorizada)
                    {
                        await AdicionarComandoAsync(mensagem, "AutorizarExecucao", saga.VersaoOrcamento, "Execucao",
                            new { PreparacaoId = saga.PreparacaoId!.Value, VersaoOrcamento = saga.VersaoOrcamento }, ct);
                    }
                    else
                    {
                        await AdicionarComandoAsync(mensagem, "CancelarPreparacao", saga.VersaoPreparacao, "Execucao",
                            new { PreparacaoId = saga.PreparacaoId!.Value, Motivo = "Orçamento recusado" }, ct);
                        await AdicionarComandoAsync(mensagem, "CancelarOrcamento", saga.VersaoOrcamento, "OrcamentoPagamento",
                            new CancelarOrcamentoDados(saga.OrcamentoId, saga.VersaoOrcamento, "Orçamento recusado"), ct);
                    }
                }
                else if (dados is OrcamentoCanceladoDados cancelado)
                {
                    if (cancelado.OrcamentoId.HasValue && saga.OrcamentoId.HasValue && cancelado.OrcamentoId != saga.OrcamentoId)
                    { throw new ContratoMensagemException("ORCAMENTO_DIVERGENTE"); }
                    saga.ConfirmarCancelamentoOrcamento(versao);
                }
                else if (dados is PreparacaoCanceladaDados preparacao)
                {
                    saga.ConfirmarCancelamentoPreparacao(preparacao.PreparacaoId, versao);
                }
            }
            await _recebimentos.RegistrarAsync(mensagem.MensagemId, chave, hash, ct);
        }, cancellationToken);
    }
    private Task AdicionarComandoAsync<T>(MensagemIntegracao origem, string tipo, int versao, string destino, T dados, CancellationToken ct) =>
        _sagas.AdicionarComandoAsync(new(Guid.CreateVersion7(), tipo, 1, origem.SagaId, origem.OrdemServicoId, origem.CorrelationId,
            origem.MensagemId, _relogio.GetUtcNow(), JsonSerializer.SerializeToElement(dados, JsonContrato.Options), origem.TraceContext), versao, destino, ct);
}
