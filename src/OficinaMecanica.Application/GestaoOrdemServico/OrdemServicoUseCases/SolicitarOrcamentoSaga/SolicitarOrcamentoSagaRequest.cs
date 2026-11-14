using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;

public sealed record SolicitarOrcamentoSagaRequest(
    Guid SagaId,
    Guid OrdemServicoId,
    Guid OperacaoId,
    string CorrelationId,
    GerarOrcamentoDados Dados);
