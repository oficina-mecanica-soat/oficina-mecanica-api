using FluentValidation;
using OficinaMecanica.Application.Common;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;
using OficinaMecanica.Application.GestaoOrdemServico.ValidationMessages;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoDaOrdemServico;

public sealed class SolicitarOrcamentoDaOrdemServicoUseCase
{
    private readonly ISagaIntegracaoRepository _sagas;
    private readonly ISnapshotOrcamentoRepository _snapshots;
    private readonly IRecebimentoMensagemRepository _recebimentos;
    private readonly SolicitarOrcamentoSagaUseCase _solicitar;
    private readonly IValidator<SolicitarOrcamentoDaOrdemServicoRequest> _validator;

    public SolicitarOrcamentoDaOrdemServicoUseCase(ISagaIntegracaoRepository sagas, ISnapshotOrcamentoRepository snapshots,
        IRecebimentoMensagemRepository recebimentos, SolicitarOrcamentoSagaUseCase solicitar,
        IValidator<SolicitarOrcamentoDaOrdemServicoRequest> validator)
    {
        _sagas = sagas;
        _snapshots = snapshots;
        _recebimentos = recebimentos;
        _solicitar = solicitar;
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
        await _recebimentos.ExecutarEmTransacaoAsync(async ct =>
        {
            var saga = await _sagas.ObterAsync(request.SagaId, request.OrdemServicoId, ct);
            if (saga is null)
            {
                return;
            }
            var anterior = await _sagas.ObterComandoAsync("GerarOrcamento", request.OrdemServicoId, saga.VersaoOrcamento, ct);
            // Reentrega retoma o snapshot persistido, sem reler preços/descrições modificados.
            var snapshot = anterior is null
                ? await _snapshots.ObterAsync(request.OrdemServicoId, saga.VersaoOrcamento, ct)
                : JsonContrato.Ler<GerarOrcamentoDados>(anterior.Dados.GetRawText());
            if (snapshot is null)
            {
                return;
            }
            resultado = await _solicitar.ExecuteAsync(new(request.SagaId, request.OrdemServicoId, request.OperacaoId,
                request.CorrelationId, snapshot), ct);
        }, cancellationToken);
        return resultado;
    }
}
