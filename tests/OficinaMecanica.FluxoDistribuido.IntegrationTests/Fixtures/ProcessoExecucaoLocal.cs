using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;

namespace OficinaMecanica.FluxoDistribuido.IntegrationTests.Fixtures;

internal sealed class ProcessoExecucaoLocal(string endpoint, string tabela, string diretorio) : IAsyncDisposable
{
    private readonly string _chave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private Process? _processo;
    private Task? _saida;
    private Task? _erro;

    public int Id => _processo?.Id ?? throw new InvalidOperationException("Processo não iniciado.");

    public void ConferirAtivo()
    {
        if (_processo is null || _processo.HasExited)
            throw new InvalidOperationException("O processo do Execução encerrou antes do ponto de interrupção esperado.");
    }

    public async Task IniciarAsync()
    {
        if (_processo is not null) throw new InvalidOperationException("Encerre o processo anterior antes de reiniciar.");
        var assembly = typeof(ProcessoExecucaoLocal).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(atributo => atributo.Key == "ExecucaoApiDll").Value!;
        if (!File.Exists(assembly)) throw new FileNotFoundException("API Execução não foi compilada pela suíte.", assembly);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var porta = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var inicio = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(assembly)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        inicio.ArgumentList.Add(assembly);
        foreach (var nome in new[] { "AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY", "AWS_SESSION_TOKEN", "AWS_PROFILE", "AWS_ROLE_ARN", "AWS_WEB_IDENTITY_TOKEN_FILE" })
            inicio.Environment.Remove(nome);
        foreach (var (nome, valor) in new Dictionary<string, string>
        {
            ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{porta}",
            ["ASPNETCORE_ENVIRONMENT"] = "Development", ["DOTNET_ENVIRONMENT"] = "Development",
            ["ProcessamentoLocal__Habilitado"] = "true", ["ProcessamentoRemoto__Habilitado"] = "false",
            ["ProcessamentoLocal__Endpoint"] = endpoint, ["ProcessamentoLocal__Tabela"] = tabela,
            ["ProcessamentoLocal__Diretorio"] = diretorio,
            ["Jwt__Secret"] = _chave, ["Jwt__Issuer"] = "oficina-mecanica-auth", ["Jwt__Audience"] = "oficina-mecanica-api",
            ["DD_TRACE_ENABLED"] = "false", ["DD_PROFILING_ENABLED"] = "false", ["CORECLR_ENABLE_PROFILING"] = "0",
            ["AWS_EC2_METADATA_DISABLED"] = "true"
        }) inicio.Environment[nome] = valor;
        _processo = Process.Start(inicio) ?? throw new InvalidOperationException("Não foi possível iniciar o Execução.");
        // Drenar sem registrar saída que possa conter configuração ou dados de autenticação.
        _saida = _processo.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        _erro = _processo.StandardError.BaseStream.CopyToAsync(Stream.Null);
        using var cliente = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{porta}"), Timeout = TimeSpan.FromSeconds(1) };
        using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!prazo.IsCancellationRequested)
        {
            ConferirAtivo();
            try
            {
                using var resposta = await cliente.GetAsync("/api/health", prazo.Token);
                if (resposta.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!prazo.IsCancellationRequested) { }
            await Task.Delay(100, prazo.Token);
        }
        throw new TimeoutException("Execução não ficou saudável dentro do prazo local.");
    }

    public async Task InterromperAsync()
    {
        if (_processo is null) return;
        try
        {
            if (!_processo.HasExited) _processo.Kill(entireProcessTree: true);
            using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _processo.WaitForExitAsync(prazo.Token);
            await Task.WhenAll(_saida!, _erro!);
        }
        finally
        {
            _processo.Dispose();
            _processo = null;
        }
    }

    public async ValueTask DisposeAsync() => await InterromperAsync();
}
