using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Application.Common;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;
using OficinaMecanica.Application.GestaoOrdemServico.ValidationMessages;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarPreparacaoOrdemServico;

public sealed class SolicitarPreparacaoOrdemServicoUseCase
{
    private readonly ISagaIntegracaoRepository _sagaIntegracaoRepository;
    private readonly ISnapshotOrcamentoRepository _snapshotOrcamentoRepository;
    private readonly IOrdemServicoRepository _ordemServicoRepository;
    private readonly IRecebimentoMensagemRepository _recebimentoMensagemRepository;
    private readonly IValidator<SolicitarPreparacaoOrdemServicoRequest> _validator;
    private readonly IValidator<SolicitarOrcamentoSagaRequest> _snapshotValidator;
    private readonly TimeProvider _relogio;
    private readonly ILogger<SolicitarPreparacaoOrdemServicoUseCase> _logger;

    public SolicitarPreparacaoOrdemServicoUseCase(ISagaIntegracaoRepository sagas, ISnapshotOrcamentoRepository snapshots,
        IOrdemServicoRepository ordens, IRecebimentoMensagemRepository recebimentos,
        IValidator<SolicitarPreparacaoOrdemServicoRequest> validator, IValidator<SolicitarOrcamentoSagaRequest> snapshotValidator,
        TimeProvider relogio, ILogger<SolicitarPreparacaoOrdemServicoUseCase> logger)
    {
        _sagaIntegracaoRepository = sagas;
        _snapshotOrcamentoRepository = snapshots;
        _ordemServicoRepository = ordens;
        _recebimentoMensagemRepository = recebimentos;
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
        await _recebimentoMensagemRepository.ExecutarEmTransacaoAsync(async cancellationTokenTransacao =>
        {
            var saga = await _sagaIntegracaoRepository.ObterPorOrdemAsync(request.OrdemServicoId, cancellationTokenTransacao);
            MensagemIntegracao mensagem;
            if (saga is not null)
            {
                var anterior = await _sagaIntegracaoRepository.ObterComandoAsync("PrepararExecucao", request.OrdemServicoId, saga.VersaoPreparacao, cancellationTokenTransacao)
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
                var ordem = await _ordemServicoRepository.ObterPorIdAsync(request.OrdemServicoId, cancellationTokenTransacao);
                if (ordem is null)
                {
                    return;
                }
                if (ordem.Status != StatusOrdemServico.Recebida)
                {
                    resultado = Result<Guid>.Falha(SagaIntegracaoValidationMessages.OrdemIncompativel, TipoErro.Validacao);
                    return;
                }
                var snapshot = await _snapshotOrcamentoRepository.ObterAsync(ordem.Id, 1, cancellationTokenTransacao)
                    ?? throw new ContratoMensagemException("SNAPSHOT_NAO_ENCONTRADO");
                saga = SagaOrdemServico.Iniciar(ordem.Id, 1, 1);
                var validacaoSnapshot = await _snapshotValidator.ValidateAsync(new(saga.Id, ordem.Id,
                    request.OperacaoId, request.CorrelationId, snapshot), cancellationTokenTransacao);
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
                await _sagaIntegracaoRepository.AdicionarAsync(saga, snapshot, cancellationTokenTransacao);
                await _sagaIntegracaoRepository.AdicionarComandoAsync(mensagem, saga.VersaoPreparacao, "Execucao", cancellationTokenTransacao);
            }
            var chave = $"PrepararExecucao:{request.OrdemServicoId:D}:{saga.VersaoPreparacao}";
            var hash = JsonContrato.Hash(mensagem, JsonContrato.Ler<PrepararExecucaoDados>(mensagem.Dados.GetRawText()));
            await _recebimentoMensagemRepository.VerificarRepeticaoAsync(request.OperacaoId, chave, hash, cancellationTokenTransacao);
            await _recebimentoMensagemRepository.RegistrarAsync(request.OperacaoId, chave, hash, cancellationTokenTransacao);
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
