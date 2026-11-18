using System.Text.Json;
using FluentAssertions;

namespace OficinaMecanica.FluxoDistribuido.IntegrationTests.Fixtures;

internal sealed class FalhasPublicacao
{
    private readonly Dictionary<(string Origem, Guid Id), (string Corpo, int Tentativas)> _mensagens = [];

    public bool HaReenvioPendente => _mensagens.Values.Any(mensagem => mensagem.Tentativas == 1);

    public bool Registrar(string origem, string corpo)
    {
        using var documento = JsonDocument.Parse(corpo);
        var chave = (origem, documento.RootElement.GetProperty("mensagemId").GetGuid());
        if (_mensagens.TryGetValue(chave, out var anterior))
        {
            corpo.Should().Be(anterior.Corpo, "a retomada deve preservar o envelope inteiro");
            _mensagens[chave] = (corpo, anterior.Tentativas + 1);
            return false;
        }
        _mensagens.Add(chave, (corpo, 1));
        return true;
    }

    public void ConferirRetomada(params string[] origens)
    {
        _mensagens.Should().NotBeEmpty();
        _mensagens.Values.Should().OnlyContain(mensagem => mensagem.Tentativas == 2);
        _mensagens.Keys.Select(chave => chave.Origem).Distinct().Should().BeEquivalentTo(origens);
    }
}
