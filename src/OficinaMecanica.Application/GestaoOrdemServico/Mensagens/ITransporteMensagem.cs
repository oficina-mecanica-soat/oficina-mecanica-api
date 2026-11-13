namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens;

public interface ITransporteMensagem
{
    Task PublicarAsync(string destino, string corpo, CancellationToken cancellationToken);
}
