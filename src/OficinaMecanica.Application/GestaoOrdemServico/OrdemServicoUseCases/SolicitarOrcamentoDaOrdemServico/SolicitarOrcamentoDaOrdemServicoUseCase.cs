using FluentValidation;
using OficinaMecanica.Application.Common;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;
using OficinaMecanica.Application.GestaoOrdemServico.ValidationMessages;
using OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoDaOrdemServico;

public sealed class SolicitarOrcamentoDaOrdemServicoUseCase
{
    private readonly ISagaOrdemServicoRepository _sagaOrdemServicoRepository;
    private readonly IOutbox _outbox;
    private readonly IConsultaSnapshotOrcamento _consultaSnapshotOrcamento;
    private readonly IInbox _inbox;
    private readonly SolicitarOrcamentoSagaUseCase _solicitarOrcamentoSagaUseCase;
    private readonly IValidator<SolicitarOrcamentoDaOrdemServicoRequest> _validator;

    public SolicitarOrcamentoDaOrdemServicoUseCase(
        ISagaOrdemServicoRepository sagaOrdemServicoRepository,
        IOutbox outbox,
        IConsultaSnapshotOrcamento consultaSnapshotOrcamento,
        IInbox inbox,
        SolicitarOrcamentoSagaUseCase solicitar,
        IValidator<SolicitarOrcamentoDaOrdemServicoRequest> validator)
    {
        _sagaOrdemServicoRepository = sagaOrdemServicoRepository;
        _outbox = outbox;
        _consultaSnapshotOrcamento = consultaSnapshotOrcamento;
        _inbox = inbox;
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
        await _inbox.ExecutarEmTransacaoAsync(async cancellationTokenTransacao =>
        {
            var saga = await _sagaOrdemServicoRepository.ObterAsync(request.SagaId, request.OrdemServicoId, cancellationTokenTransacao);
            if (saga is null)
            {
                return;
            }
            var anterior = await _outbox.ObterComandoAsync("GerarOrcamento", request.OrdemServicoId, saga.VersaoOrcamento, cancellationTokenTransacao);
            // Reentrega retoma o snapshot persistido, sem reler preços/descrições modificados.
            var snapshot = anterior is null
                ? await _consultaSnapshotOrcamento.ObterAsync(request.OrdemServicoId, saga.VersaoOrcamento, cancellationTokenTransacao)
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
