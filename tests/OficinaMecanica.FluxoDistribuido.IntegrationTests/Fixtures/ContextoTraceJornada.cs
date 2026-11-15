using System.Text.Json.Nodes;
using FluentAssertions;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;

namespace OficinaMecanica.FluxoDistribuido.IntegrationTests.Fixtures;

internal sealed class ContextoTraceJornada(string cenario)
{
    private const string Traceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
    private readonly HashSet<string> _tipos = [];

    public Dictionary<string, string>? Origem { get; } = cenario switch
    {
        "ausente" => null,
        "valido" => new() { ["traceparent"] = Traceparent },
        "campos-proibidos" => new()
        {
            ["traceparent"] = Traceparent, ["baggage"] = "dado-sintetico-proibido",
            ["Authorization"] = "credencial-sintetica-proibida", ["tracestate"] = "campo-proibido"
        },
        "invalido" => new() { ["traceparent"] = "invalido", ["baggage"] = "dado-sintetico-proibido" },
        _ => throw new ArgumentOutOfRangeException(nameof(cenario))
    };

    public void Conferir(string corpo)
    {
        var mensagem = JsonContrato.LerEnvelope(corpo);
        _tipos.Add(mensagem.Tipo);
        if (mensagem.Tipo == "PrepararExecucao")
        {
            mensagem.TraceContext.Should().BeEquivalentTo(ContextoTrace.Filtrar(Origem));
            return;
        }
        var bruto = JsonContrato.Ler<MensagemIntegracao>(corpo).TraceContext;
        bruto.Should().BeEquivalentTo(ContextoTrace.Filtrar(bruto), "somente traceparent válido pode sair dos participantes");
        if (ContextoTrace.Filtrar(Origem) is not null)
        {
            bruto.Should().NotBeNull();
            bruto!["traceparent"].Substring(3, 32).Should().Be(Traceparent.Substring(3, 32));
        }
    }

    public static string AlterarReentrega(string corpo)
    {
        var mensagem = JsonNode.Parse(corpo)!;
        mensagem["traceContext"] = new JsonObject
        {
            ["traceparent"] = "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01",
            ["baggage"] = "outra-informacao-sintetica-proibida"
        };
        return mensagem.ToJsonString();
    }

    public void ConferirJornada() => _tipos.Should().Contain([
        "PrepararExecucao", "ExecucaoPreparada", "GerarOrcamento", "OrcamentoGerado",
        "OrcamentoDecidido", "AutorizarExecucao", "ExecucaoIniciada", "ExecucaoFinalizada",
        "CriarCobranca", "CobrancaCriada", "PagamentoConfirmado"]);
}
