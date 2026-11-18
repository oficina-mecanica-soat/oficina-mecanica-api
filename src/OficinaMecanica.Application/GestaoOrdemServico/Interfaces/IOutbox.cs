using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;

namespace OficinaMecanica.Application.GestaoOrdemServico.Interfaces;

public interface IOutbox
{
    Task<MensagemIntegracao?> ObterComandoAsync(string tipo, Guid ordemServicoId, int versao, CancellationToken cancellationToken);
    Task AdicionarComandoAsync(MensagemIntegracao mensagem, int versao, string destino, CancellationToken cancellationToken);
}
