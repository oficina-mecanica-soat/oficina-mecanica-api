using FluentValidation;
using OficinaMecanica.Application.Common;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;
using OficinaMecanica.Application.GestaoOrdemServico.ValidationMessages;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoDaOrdemServico;

public sealed class SolicitarOrcamentoDaOrdemServicoUseCase
{
    private readonly ISagaIntegracaoRepository _sagaIntegracaoRepository;
    private readonly ISnapshotOrcamentoRepository _snapshotOrcamentoRepository;
    private readonly IRecebimentoMensagemRepository _recebimentoMensagemRepository;
    private readonly SolicitarOrcamentoSagaUseCase _solicitarOrcamentoSagaUseCase;
    private readonly IValidator<SolicitarOrcamentoDaOrdemServicoRequest> _validator;

    public SolicitarOrcamentoDaOrdemServicoUseCase(ISagaIntegracaoRepository sagas, ISnapshotOrcamentoRepository snapshots,
        IRecebimentoMensagemRepository recebimentos, SolicitarOrcamentoSagaUseCase solicitar,
        IValidator<SolicitarOrcamentoDaOrdemServicoRequest> validator)
    {
        _sagaIntegracaoRepository = sagas;
        _snapshotOrcamentoRepository = snapshots;
        _recebimentoMensagemRepository = recebimentos;
        _solicitarOrcamentoSagaUseCase = solicitar;
        _validator = validator;
    }

    public async Task<Result<Guid>> ExecuteAsync(SolicitarOrcamentoDaOrdemServicoRequest request, CancellationToken cancellationToken = default)
    {
        var validacao = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacao.IsValid)
        {
            return Result<Guid>.Falha(validacao.ObterMensagensErro(), TipoErro.Validacao);
        }

        var resultado = Result<Guid>.Falha(SagaIntegracaoValidationMessages.SagaNaoEncontrada, TipoErro.NaoEncontrado);
        await _recebimentoMensagemRepository.ExecutarEmTransacaoAsync(async cancellationTokenTransacao =>
        {
            var saga = await _sagaIntegracaoRepository.ObterAsync(request.SagaId, request.OrdemServicoId, cancellationTokenTransacao);
            if (saga is null)
            {
                return;
            }
            var anterior = await _sagaIntegracaoRepository.ObterComandoAsync("GerarOrcamento", request.OrdemServicoId, saga.VersaoOrcamento, cancellationTokenTransacao);
            // Reentrega retoma o snapshot persistido, sem reler preços/descrições modificados.
            var snapshot = anterior is null
                ? await _snapshotOrcamentoRepository.ObterAsync(request.OrdemServicoId, saga.VersaoOrcamento, cancellationTokenTransacao)
                : JsonContrato.Ler<GerarOrcamentoDados>(anterior.Dados.GetRawText());
            if (snapshot is null)
            {
                return;
            }
            resultado = await _solicitarOrcamentoSagaUseCase.ExecuteAsync(new(request.SagaId, request.OrdemServicoId, request.OperacaoId,
                request.CorrelationId, snapshot), cancellationTokenTransacao);
        }, cancellationToken);
        return resultado;
    }
}
