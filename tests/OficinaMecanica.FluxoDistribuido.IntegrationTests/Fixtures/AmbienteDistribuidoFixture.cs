using Amazon.DynamoDBv2;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Execucao.Infrastructure.Mensagens;
using OficinaMecanica.Execucao.Infrastructure.Persistencia;
using OficinaMecanica.Infrastructure.Persistence;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace OficinaMecanica.FluxoDistribuido.IntegrationTests.Fixtures;

public sealed class AmbienteDistribuidoFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly IContainer _dynamo = new ContainerBuilder("amazon/dynamodb-local:3.1.0")
        .WithPortBinding(8000, true)
        .WithCommand("-jar", "DynamoDBLocal.jar", "-inMemory", "-sharedDb")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8000))
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sql.StartAsync(), _dynamo.StartAsync());
    }

    internal async Task<JornadaDistribuida> CriarJornadaAsync(bool falharPublicacao = false)
    {
        var id = Guid.NewGuid().ToString("N");
        var diretorio = Path.Combine(Path.GetTempPath(), "oficina-bdd-" + id);
        var conexaoOs = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "Os_" + id }.ConnectionString;
        var conexaoOrcamento = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "Orcamento_" + id }.ConnectionString;
        var provedor = new ProvedorPagamentoTeste();
        var logs = new LogsJornada();
        var os = CriarServicosSql(conexaoOs, true, provedor, logs);
        var orcamento = CriarServicosSql(conexaoOrcamento, false, provedor, logs);
        var endpoint = $"http://{_dynamo.Hostname}:{_dynamo.GetMappedPublicPort(8000)}";
        var clienteDynamo = DynamoDbLocal.CriarClient(endpoint);
        var tabela = "jornada-" + id;

        var services = new ServiceCollection().AddLogging(builder => builder.AddProvider(logs.CriarProvedor()));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProcessamentoLocal:Endpoint"] = endpoint,
            ["ProcessamentoLocal:Tabela"] = tabela,
            ["ProcessamentoLocal:Diretorio"] = diretorio
        }).Build();
        OficinaMecanica.Execucao.Application.DependencyInjection.AddApplication(services);
        OficinaMecanica.Execucao.Infrastructure.DependencyInjection.AddInfrastructure(services, config);
        services.AddSingleton(clienteDynamo);
        services.AddScoped<OficinaMecanica.Execucao.Application.Mensagens.ITransporteResultado>(_ =>
            new TransporteArquivos(Path.Combine(diretorio, "OrdemServico", "entrada")));
        var execucao = services.BuildServiceProvider();

        var jornada = new JornadaDistribuida(os, orcamento, execucao, clienteDynamo, diretorio, provedor, falharPublicacao, logs);
        try
        {
            await DynamoDbLocal.PrepararTabelaAsync(clienteDynamo, tabela, CancellationToken.None);
            await using (var scope = os.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<OficinaMecanicaDbContext>().Database.MigrateAsync();
            await using (var scope = orcamento.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<OrcamentoPagamentoDbContext>().Database.MigrateAsync();
            return jornada;
        }
        catch
        {
            await jornada.DisposeAsync();
            throw;
        }
    }

    private static ServiceProvider CriarServicosSql(string conexao, bool ordemServico, ProvedorPagamentoTeste provedor, LogsJornada logs)
    {
        var services = new ServiceCollection().AddLogging(builder => builder.AddProvider(logs.CriarProvedor()));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = conexao
        }).Build();
        if (ordemServico)
        {
            OficinaMecanica.Application.DependencyInjection.AddApplication(services);
            OficinaMecanica.Infrastructure.DependencyInjection.AddInfrastructure(services, config);
        }
        else
        {
            OficinaMecanica.OrcamentoPagamento.Application.DependencyInjection.AddApplication(services);
            OficinaMecanica.OrcamentoPagamento.Infrastructure.DependencyInjection.AddInfrastructure(services, config);
            services.AddSingleton<IProvedorPagamento>(provedor);
        }
        return services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        await _dynamo.DisposeAsync();
        await _sql.DisposeAsync();
    }
}
