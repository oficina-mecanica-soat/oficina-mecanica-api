using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoPagamento;

public sealed class ProcessarResultadoPagamentoUseCase
{
    private readonly IInbox _inbox;
    private readonly IOutbox _outbox;
    private readonly ISagaOrdemServicoRepository _sagaOrdemServicoRepository;

    public ProcessarResultadoPagamentoUseCase(
        IInbox inbox,
        IOutbox outbox,
        ISagaOrdemServicoRepository sagaOrdemServicoRepository)
    {
        _inbox = inbox;
        _outbox = outbox;
        _sagaOrdemServicoRepository = sagaOrdemServicoRepository;
    }

    public async Task ExecuteAsync(string corpo, CancellationToken cancellationToken = default)
    {
        var mensagem = JsonContrato.LerEnvelope(corpo);
        if (mensagem.Tipo is not ("CobrancaCriada" or "PagamentoConfirmado" or "PagamentoRecusado"))
        {
            throw new ContratoMensagemException("TIPO_DESCONHECIDO");
        }
        var dados = JsonContrato.Ler<ResultadoPagamentoDados>(mensagem.Dados.GetRawText());
        if (dados.PagamentoId == Guid.Empty || dados.OrcamentoId == Guid.Empty || dados.Versao <= 0
            || dados.Valor <= 0 || decimal.Round(dados.Valor, 2) != dados.Valor)
        {
            throw new ContratoMensagemException("RESULTADO_PAGAMENTO_INVALIDO");
        }
        var chave = $"{mensagem.Tipo}:{mensagem.OrdemServicoId:D}:{dados.Versao}";
        var hash = JsonContrato.Hash(mensagem, dados);
        await _inbox.ExecutarEmTransacaoAsync(async ct =>
        {
            if (!await _inbox.VerificarRepeticaoAsync(mensagem.MensagemId, chave, hash, ct))
            {
                var saga = await _sagaOrdemServicoRepository.ObterAsync(mensagem.SagaId, mensagem.OrdemServicoId, ct)
                    ?? throw new ContratoMensagemException("SAGA_NAO_ENCONTRADA");
                var comando = await _outbox.ObterComandoAsync("CriarCobranca", saga.OrdemServicoId, dados.Versao, ct)
                    ?? throw new ContratoMensagemException("COBRANCA_NAO_SOLICITADA");
                var solicitado = JsonContrato.Ler<CriarCobrancaDados>(comando.Dados.GetRawText());
                if (comando.SagaId != mensagem.SagaId || comando.CorrelationId != mensagem.CorrelationId
                    || solicitado.OrcamentoId != dados.OrcamentoId || solicitado.Versao != dados.Versao
                    || saga.OrcamentoId != dados.OrcamentoId || saga.VersaoOrcamento != dados.Versao || saga.ValorOrcamento != dados.Valor
                    || mensagem.Tipo == "CobrancaCriada" && mensagem.CausationId != comando.MensagemId)
                {
                    throw new ContratoMensagemException("COBRANCA_DIVERGENTE");
                }
                if (saga.Estado is not (EstadoSagaOrdemServico.AguardandoPagamento or EstadoSagaOrdemServico.PagamentoConfirmado))
                {
                    throw new ContratoMensagemException("PAGAMENTO_FORA_DO_ESTADO_ESPERADO");
                }
                if (mensagem.Tipo == "PagamentoConfirmado")
                {
                    saga.RegistrarPagamento(dados.OrcamentoId, dados.Versao, dados.PagamentoId, dados.Valor);
                }
                else if (saga.PagamentoId.HasValue && saga.PagamentoId != dados.PagamentoId)
                {
                    throw new ContratoMensagemException("PAGAMENTO_DIVERGENTE");
                }
            }
            await _inbox.RegistrarAsync(mensagem.MensagemId, chave, hash, ct);
        }, cancellationToken);
    }
}
