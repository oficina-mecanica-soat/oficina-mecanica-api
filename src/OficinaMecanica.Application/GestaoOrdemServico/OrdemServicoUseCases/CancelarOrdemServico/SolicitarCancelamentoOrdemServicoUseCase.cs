using System.Text.Json;
using FluentValidation;
using OficinaMecanica.Application.Common;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Domain.GestaoOrdemServico.Messages;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.CancelarOrdemServico;

public sealed class SolicitarCancelamentoOrdemServicoUseCase
{
    private readonly ISagaOrdemServicoRepository _sagaOrdemServicoRepository;
    private readonly IInbox _inbox;
    private readonly IOutbox _outbox;
    private readonly IValidator<CancelarOrdemServicoRequest> _validator;
    private readonly TimeProvider _relogio;

    public SolicitarCancelamentoOrdemServicoUseCase(ISagaOrdemServicoRepository sagaOrdemServicoRepository,
        IInbox inbox, IOutbox outbox, IValidator<CancelarOrdemServicoRequest> validator, TimeProvider relogio)
    {
        _sagaOrdemServicoRepository = sagaOrdemServicoRepository;
        _inbox = inbox;
        _outbox = outbox;
        _validator = validator;
        _relogio = relogio;
    }

    public async Task<Result<Guid>> ExecuteAsync(CancelarOrdemServicoRequest request, CancellationToken cancellationToken = default)
    {
        var validacao = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacao.IsValid)
        {
            return Result<Guid>.Falha(validacao.ObterMensagensErro(), TipoErro.Validacao);
        }

        var resultado = Result<Guid>.Falha(OrdemServicoErrorMessages.OrdemServicoNaoEncontrada, TipoErro.NaoEncontrado);
        await _inbox.ExecutarEmTransacaoAsync(async ct =>
        {
            var saga = await _sagaOrdemServicoRepository.ObterPorOrdemAsync(request.OrdemServicoId, ct);
            if (saga is null)
            {
                return;
            }

            saga.IniciarCompensacao();
            var preparacao = await _outbox.ObterComandoAsync("PrepararExecucao", saga.OrdemServicoId, saga.VersaoPreparacao, ct)
                ?? throw new ContratoMensagemException("PREPARACAO_NAO_SOLICITADA");
            var motivo = request.Motivo == MotivoCancelamentoOrdemServico.ReprovacaoOrcamento
                ? "Orçamento recusado" : "Estoque insuficiente";
            var anterior = await _outbox.ObterComandoAsync("CancelarPreparacao", saga.OrdemServicoId, saga.VersaoPreparacao, ct);
            if (anterior is null)
            {
                await _outbox.AdicionarComandoAsync(new(Guid.CreateVersion7(), "CancelarPreparacao", 1, saga.Id,
                    saga.OrdemServicoId, preparacao.CorrelationId, preparacao.MensagemId, _relogio.GetUtcNow(),
                    JsonSerializer.SerializeToElement(new { PreparacaoId = saga.PreparacaoId!.Value, Motivo = motivo }, JsonContrato.Options),
                    preparacao.TraceContext), saga.VersaoPreparacao, "Execucao", ct);
                await _outbox.AdicionarComandoAsync(new(Guid.CreateVersion7(), "CancelarOrcamento", 1, saga.Id,
                    saga.OrdemServicoId, preparacao.CorrelationId, preparacao.MensagemId, _relogio.GetUtcNow(),
                    JsonSerializer.SerializeToElement(new CancelarOrcamentoDados(saga.OrcamentoId, saga.VersaoOrcamento, motivo), JsonContrato.Options),
                    preparacao.TraceContext), saga.VersaoOrcamento, "OrcamentoPagamento", ct);
            }
            else if (anterior.Dados.GetProperty("motivo").GetString() != motivo)
            {
                throw new ContratoMensagemException("OPERACAO_DIVERGENTE");
            }

            resultado = Result<Guid>.Ok(saga.Id);
        }, cancellationToken);
        return resultado;
    }
}
