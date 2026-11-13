using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Application.GestaoOrdemServico.ValidationMessages;
using OficinaMecanica.Application.Common;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;

public sealed class SolicitarOrcamentoSagaUseCase
{
    private readonly ISagaIntegracaoRepository _sagas;
    private readonly IRecebimentoMensagemRepository _recebimentos;
    private readonly IValidator<SolicitarOrcamentoSagaRequest> _validator;
    private readonly TimeProvider _relogio;
    private readonly ILogger<SolicitarOrcamentoSagaUseCase> _logger;
    public SolicitarOrcamentoSagaUseCase(ISagaIntegracaoRepository sagas, IRecebimentoMensagemRepository recebimentos,
        IValidator<SolicitarOrcamentoSagaRequest> validator, TimeProvider relogio, ILogger<SolicitarOrcamentoSagaUseCase> logger)
    { _sagas = sagas; _recebimentos = recebimentos; _validator = validator; _relogio = relogio; _logger = logger; }
    public async Task<Result<Guid>> ExecuteAsync(SolicitarOrcamentoSagaRequest request, CancellationToken cancellationToken = default)
    {
        var validacao = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacao.IsValid) { return Result<Guid>.Falha(validacao.ObterMensagensErro(), TipoErro.Validacao); }
        var dados = request.Dados with { Servicos = request.Dados.Servicos.OrderBy(s => s.ServicoId).ToArray(), Itens = request.Dados.Itens.OrderBy(i => i.ItemId).ThenBy(i => i.Descricao, StringComparer.Ordinal).ThenBy(i => i.ValorUnitario).ThenBy(i => i.Quantidade).ToArray() };
        var mensagem = new MensagemIntegracao(Guid.CreateVersion7(), "GerarOrcamento", 1, request.SagaId, request.OrdemServicoId, request.CorrelationId,
            request.OperacaoId, _relogio.GetUtcNow(), JsonSerializer.SerializeToElement(dados, JsonContrato.Options));
        var chave = $"GerarOrcamento:{request.OrdemServicoId:D}:{dados.Versao}";
        var hash = JsonContrato.Hash(mensagem, dados);
        var resultado = Result<Guid>.Falha(SagaIntegracaoValidationMessages.SagaNaoEncontrada, TipoErro.NaoEncontrado);
        await _recebimentos.ExecutarEmTransacaoAsync(async ct =>
        {
            var repetida = await _recebimentos.VerificarRepeticaoAsync(request.OperacaoId, chave, hash, ct);
            var saga = await _sagas.ObterAsync(request.SagaId, request.OrdemServicoId, ct);
            if (saga is null) { return; }
            if (!repetida)
            {
                if (saga.Estado != EstadoSagaOrdemServico.AguardandoOrcamento || saga.VersaoOrcamento != dados.Versao)
                { throw new ContratoMensagemException("SAGA_INCOMPATIVEL"); }
                await _sagas.AdicionarComandoAsync(mensagem, dados.Versao, "OrcamentoPagamento", ct);
            }
            await _recebimentos.RegistrarAsync(request.OperacaoId, chave, hash, ct);
            var comando = await _sagas.ObterComandoAsync("GerarOrcamento", request.OrdemServicoId, dados.Versao, ct);
            resultado = Result<Guid>.Ok(comando?.MensagemId ?? mensagem.MensagemId);
        }, cancellationToken);
        if (resultado.Sucesso)
        {
            _logger.LogInformation("Orçamento solicitado para a OS {OrdemServicoId}, Saga {SagaId}. {operation} {bounded_context}",
                request.OrdemServicoId, request.SagaId, "SolicitarOrcamentoSaga", "GestaoOrdemServico");
        }
        return resultado;
    }
}
