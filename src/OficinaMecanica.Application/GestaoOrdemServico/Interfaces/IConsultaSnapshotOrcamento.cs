using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;

namespace OficinaMecanica.Application.GestaoOrdemServico.Interfaces;

public interface IConsultaSnapshotOrcamento
{
    Task<GerarOrcamentoDados?> ObterAsync(Guid ordemServicoId, int versao, CancellationToken cancellationToken);
}
