namespace OficinaMecanica.FluxoDistribuido.IntegrationTests.Fixtures;

internal sealed class RelogioPublicacao : TimeProvider
{
    private DateTimeOffset _agora = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _agora;

    public void Avancar() => _agora = _agora.AddSeconds(3);
}
