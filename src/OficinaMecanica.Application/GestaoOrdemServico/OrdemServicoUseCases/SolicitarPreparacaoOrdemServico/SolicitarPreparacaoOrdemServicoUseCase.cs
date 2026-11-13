using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Application.Common;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;
using OficinaMecanica.Application.GestaoOrdemServico.ValidationMessages;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarPreparacaoOrdemServico;

public sealed class SolicitarPreparacaoOrdemServicoUseCase
{
    private readonly ISagaIntegracaoRepository _sagas;
    private readonly ISnapshotOrcamentoRepository _snapshots;
    private readonly IOrdemServicoRepository _ordens;
    private readonly IRecebimentoMensagemRepository _recebimentos;
    private readonly IValidator<SolicitarPreparacaoOrdemServicoRequest> _validator;
    private readonly IValidator<SolicitarOrcamentoSagaRequest> _snapshotValidator;
    private readonly TimeProvider _relogio;
    private readonly ILogger<SolicitarPreparacaoOrdemServicoUseCase> _logger;

    public SolicitarPreparacaoOrdemServicoUseCase(ISagaIntegracaoRepository sagas, ISnapshotOrcamentoRepository snapshots,
        IOrdemServicoRepository ordens, IRecebimentoMensagemRepository recebimentos,
        IValidator<SolicitarPreparacaoOrdemServicoRequest> validator, IValidator<SolicitarOrcamentoSagaRequest> snapshotValidator,
        TimeProvider relogio, ILogger<SolicitarPreparacaoOrdemServicoUseCase> logger)
    {
        _sagas = sagas;
        _snapshots = snapshots;
        _ordens = ordens;
        _recebimentos = recebimentos;
        _validator = validator;
        _snapshotValidator = snapshotValidator;
        _relogio = relogio;
        _logger = logger;
    }
    public async Task<Result<Guid>> ExecuteAsync(SolicitarPreparacaoOrdemServicoRequest request, CancellationToken cancellationToken = default)
    {
        var validacao = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacao.IsValid)
        {
            return Result<Guid>.Falha(validacao.ObterMensagensErro(), TipoErro.Validacao);
        }

        var resultado = Result<Guid>.Falha(SagaIntegracaoValidationMessages.OrdemNaoEncontrada, TipoErro.NaoEncontrado);
        await _recebimentos.ExecutarEmTransacaoAsync(async ct =>
        {
            var saga = await _sagas.ObterPorOrdemAsync(request.OrdemServicoId, ct);
            MensagemIntegracao mensagem;
            if (saga is not null)
            {
                var anterior = await _sagas.ObterComandoAsync("PrepararExecucao", request.OrdemServicoId, saga.VersaoPreparacao, ct)
                    ?? throw new ContratoMensagemException("PREPARACAO_NAO_SOLICITADA");
                var dadosAnteriores = JsonContrato.Ler<PrepararExecucaoDados>(anterior.Dados.GetRawText());
                if (dadosAnteriores.FilialId != request.FilialId || anterior.CorrelationId != request.CorrelationId)
                {
                    throw new ContratoMensagemException("OPERACAO_DIVERGENTE");
                }
                // Reenvio não relê catálogo, recria a Saga ou reabre uma preparação concluída.
                mensagem = anterior;
            }
            else
            {
                var ordem = await _ordens.ObterPorIdAsync(request.OrdemServicoId, ct);
                if (ordem is null) { return; }
                if (ordem.Status != StatusOrdemServico.Recebida)
                {
                    resultado = Result<Guid>.Falha(SagaIntegracaoValidationMessages.OrdemIncompativel, TipoErro.Validacao);
                    return;
                }
                var snapshot = await _snapshots.ObterAsync(ordem.Id, 1, ct)
                    ?? throw new ContratoMensagemException("SNAPSHOT_NAO_ENCONTRADO");
                saga = SagaOrdemServico.Iniciar(ordem.Id, 1, 1);
                var validacaoSnapshot = await _snapshotValidator.ValidateAsync(new(saga.Id, ordem.Id,
                    request.OperacaoId, request.CorrelationId, snapshot), ct);
                if (!validacaoSnapshot.IsValid)
                {
                    resultado = Result<Guid>.Falha(validacaoSnapshot.ObterMensagensErro(), TipoErro.Validacao);
                    return;
                }
                var itens = snapshot.Itens.GroupBy(i => i.ItemId)
                    .Select(g => new { ItemId = g.Key, Quantidade = g.Sum(i => (long)i.Quantidade) }).ToArray();
                if (itens.Any(i => i.Quantidade > int.MaxValue))
                {
                    resultado = Result<Guid>.Falha(SagaIntegracaoValidationMessages.QuantidadeInvalida, TipoErro.Validacao);
                    return;
                }
                var dados = new PrepararExecucaoDados(saga.VersaoPreparacao, request.FilialId, ordem.VeiculoId, ordem.MecanicoId,
                    itens.OrderBy(i => i.ItemId).Select(i => new ItemPreparacaoDados(i.ItemId, (int)i.Quantidade)).ToArray(), snapshot.Servicos);
                mensagem = new(Guid.CreateVersion7(), "PrepararExecucao", 1, saga.Id, ordem.Id, request.CorrelationId,
                    request.OperacaoId, _relogio.GetUtcNow(), JsonSerializer.SerializeToElement(dados, JsonContrato.Options));
                await _sagas.AdicionarAsync(saga, snapshot, ct);
                await _sagas.AdicionarComandoAsync(mensagem, saga.VersaoPreparacao, "Execucao", ct);
            }
            var chave = $"PrepararExecucao:{request.OrdemServicoId:D}:{saga.VersaoPreparacao}";
            var hash = JsonContrato.Hash(mensagem, JsonContrato.Ler<PrepararExecucaoDados>(mensagem.Dados.GetRawText()));
            await _recebimentos.VerificarRepeticaoAsync(request.OperacaoId, chave, hash, ct);
            await _recebimentos.RegistrarAsync(request.OperacaoId, chave, hash, ct);
            resultado = Result<Guid>.Ok(saga.Id);
        }, cancellationToken);
        if (resultado.Sucesso)
        {
            _logger.LogInformation("Preparação solicitada para a OS {OrdemServicoId}, Saga {SagaId}. {operation} {bounded_context}",
                request.OrdemServicoId, resultado.Valor, "SolicitarPreparacaoOrdemServico", "GestaoOrdemServico");
        }
        return resultado;
    }
}
