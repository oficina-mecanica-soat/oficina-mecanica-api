using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens;

public interface ISagaIntegracaoRepository
{
    Task<SagaOrdemServico?> ObterPorOrdemAsync(Guid ordemServicoId, CancellationToken cancellationToken);
    Task AdicionarAsync(SagaOrdemServico saga, GerarOrcamentoDados snapshot, CancellationToken cancellationToken);
    Task<GerarOrcamentoDados?> ObterSnapshotAsync(SagaOrdemServico saga, CancellationToken cancellationToken);
    Task<SagaOrdemServico?> ObterAsync(Guid sagaId, Guid ordemServicoId, CancellationToken cancellationToken);
    Task<MensagemIntegracao?> ObterComandoAsync(string tipo, Guid ordemServicoId, int versao, CancellationToken cancellationToken);
    Task AdicionarComandoAsync(MensagemIntegracao mensagem, int versao, string destino, CancellationToken cancellationToken);
}
