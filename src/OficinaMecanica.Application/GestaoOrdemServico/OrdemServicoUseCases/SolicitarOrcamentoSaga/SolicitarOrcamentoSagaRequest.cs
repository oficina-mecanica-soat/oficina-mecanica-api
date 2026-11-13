using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;

public sealed record SolicitarOrcamentoSagaRequest(Guid SagaId, Guid OrdemServicoId, Guid OperacaoId, string CorrelationId, GerarOrcamentoDados Dados);
