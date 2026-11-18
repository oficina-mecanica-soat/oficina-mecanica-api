using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace OficinaMecanica.FluxoDistribuido.IntegrationTests.Fixtures;

internal sealed class LogsJornada : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<(string Categoria, string Texto)> _eventos;
    private IExternalScopeProvider _escopos = new LoggerExternalScopeProvider();

    public LogsJornada() : this(new()) { }

    private LogsJornada(ConcurrentQueue<(string Categoria, string Texto)> eventos) => _eventos = eventos;

    // Cada LoggerFactory mantém seus próprios escopos; somente a coleta é compartilhada.
    public ILoggerProvider CriarProvedor() => new LogsJornada(_eventos);

    public ILogger CreateLogger(string categoryName) => new LoggerJornada(this, categoryName);
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _escopos = scopeProvider;
    public void Dispose() { }

    public void ConferirCarriersProibidos()
    {
        foreach (var participante in new[] { "OficinaMecanica.Application.", "OficinaMecanica.Execucao.", "OficinaMecanica.OrcamentoPagamento." })
            _eventos.Should().Contain(evento => evento.Categoria.StartsWith(participante), "a verificação exige logs dos três participantes");
        var textos = string.Join('\n', _eventos.Select(evento => evento.Texto));
        foreach (var proibido in new[] { "dado-sintetico-proibido", "credencial-sintetica-proibida", "campo-proibido", "outra-informacao-sintetica-proibida" })
            textos.Should().NotContain(proibido, "carriers descartados não devem aparecer em mensagens, estado, exceções ou escopos de log");
    }

    private static string Detalhar(object? valor) => valor switch
    {
        IEnumerable<KeyValuePair<string, object?>> campos => string.Join(';', campos.Select(campo => campo.Key + "=" + Detalhar(campo.Value))),
        IEnumerable<KeyValuePair<string, string>> campos => string.Join(';', campos.Select(campo => campo.Key + "=" + campo.Value)),
        _ => valor?.ToString() ?? string.Empty
    };

    private sealed class LoggerJornada(LogsJornada destino, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => destino._escopos.Push(state);
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var partes = new List<string> { formatter(state, exception), Detalhar(state), exception?.ToString() ?? string.Empty };
            destino._escopos.ForEachScope((escopo, textos) => textos.Add(Detalhar(escopo)), partes);
            destino._eventos.Enqueue((categoria, string.Join('\n', partes)));
        }
    }
}
