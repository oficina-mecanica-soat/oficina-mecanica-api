using System.Text.Json;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoExecucao;

public sealed class ProcessarResultadoExecucaoUseCase
{
    private readonly IInbox _inbox;
    private readonly IOutbox _outbox;
    private readonly ISagaOrdemServicoRepository _sagaOrdemServicoRepository;
    private readonly TimeProvider _relogio;

    public ProcessarResultadoExecucaoUseCase(
        IInbox inbox,
        IOutbox outbox,
        ISagaOrdemServicoRepository sagaOrdemServicoRepository,
        TimeProvider relogio)
    {
        _inbox = inbox;
        _outbox = outbox;
        _sagaOrdemServicoRepository = sagaOrdemServicoRepository;
        _relogio = relogio;
    }

    public async Task ExecuteAsync(string corpo, CancellationToken cancellationToken = default)
    {
        var mensagem = JsonContrato.LerEnvelope(corpo);
        Guid preparacaoId;
        int versaoOrcamento;
        DateTimeOffset? finalizadoEm = null;
        object dados;
        if (mensagem.Tipo == "ExecucaoIniciada")
        {
            var iniciada = JsonContrato.Ler<ExecucaoIniciadaDados>(mensagem.Dados.GetRawText());
            preparacaoId = iniciada.PreparacaoId;
            versaoOrcamento = iniciada.VersaoOrcamento;
            dados = iniciada;
        }
        else if (mensagem.Tipo == "ExecucaoFinalizada")
        {
            var finalizada = JsonContrato.Ler<ExecucaoFinalizadaDados>(mensagem.Dados.GetRawText());
            preparacaoId = finalizada.PreparacaoId;
            versaoOrcamento = finalizada.VersaoOrcamento;
            finalizadoEm = finalizada.FinalizadoEm;
            dados = finalizada;
        }
        else
        {
            throw new ContratoMensagemException("TIPO_DESCONHECIDO");
        }
        if (preparacaoId == Guid.Empty || versaoOrcamento <= 0
            || finalizadoEm.HasValue && (finalizadoEm == default(DateTimeOffset) || finalizadoEm.Value.Offset != TimeSpan.Zero))
        {
            throw new ContratoMensagemException("RESULTADO_EXECUCAO_INVALIDO");
        }

        var chave = $"{mensagem.Tipo}:{mensagem.OrdemServicoId:D}:{versaoOrcamento}";
        var hash = JsonContrato.Hash(mensagem, dados);
        await _inbox.ExecutarEmTransacaoAsync(async ct =>
        {
            if (!await _inbox.VerificarRepeticaoAsync(mensagem.MensagemId, chave, hash, ct))
            {
                var saga = await _sagaOrdemServicoRepository.ObterAsync(mensagem.SagaId, mensagem.OrdemServicoId, ct)
                    ?? throw new ContratoMensagemException("SAGA_NAO_ENCONTRADA");
                var comando = await _outbox.ObterComandoAsync("AutorizarExecucao", saga.OrdemServicoId, versaoOrcamento, ct)
                    ?? throw new ContratoMensagemException("EXECUCAO_NAO_AUTORIZADA");
                var autorizado = JsonContrato.Ler<AutorizarExecucaoDados>(comando.Dados.GetRawText());
                if (comando.SagaId != mensagem.SagaId || comando.CorrelationId != mensagem.CorrelationId
                    || autorizado.PreparacaoId != preparacaoId || autorizado.VersaoOrcamento != versaoOrcamento)
                {
                    throw new ContratoMensagemException("AUTORIZACAO_DIVERGENTE");
                }
                if (mensagem.Tipo == "ExecucaoIniciada")
                {
                    saga.RegistrarInicioFisico(preparacaoId, versaoOrcamento);
                }
                else
                {
                    saga.RegistrarFinalizacao(preparacaoId, versaoOrcamento, finalizadoEm!.Value);
                    var cobranca = new MensagemIntegracao(Guid.CreateVersion7(), "CriarCobranca", 1, saga.Id,
                        saga.OrdemServicoId, mensagem.CorrelationId, mensagem.MensagemId, _relogio.GetUtcNow(),
                        JsonSerializer.SerializeToElement(new CriarCobrancaDados(saga.OrcamentoId!.Value, saga.VersaoOrcamento), JsonContrato.Options),
                        mensagem.TraceContext);
                    await _outbox.AdicionarComandoAsync(cobranca, saga.VersaoOrcamento, "OrcamentoPagamento", ct);
                }
            }
            await _inbox.RegistrarAsync(mensagem.MensagemId, chave, hash, ct);
        }, cancellationToken);
    }
}
