namespace OficinaMecanica.Application.GestaoOrdemServico.Interfaces;

public interface IProcessadorMensagem
{
    Task ProcessarAsync(string corpo, CancellationToken cancellationToken = default);
}
