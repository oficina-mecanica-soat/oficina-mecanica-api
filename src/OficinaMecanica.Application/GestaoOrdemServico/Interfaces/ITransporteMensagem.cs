namespace OficinaMecanica.Application.GestaoOrdemServico.Interfaces;

public interface ITransporteMensagem
{
    Task PublicarAsync(string destino, string corpo, CancellationToken cancellationToken);
}
