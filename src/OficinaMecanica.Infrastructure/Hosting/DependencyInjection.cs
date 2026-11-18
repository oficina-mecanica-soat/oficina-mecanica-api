using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Infrastructure.Observabilidade;

namespace OficinaMecanica.Infrastructure.Hosting;

public static class DependencyInjection
{
    public static IServiceCollection AddProcessamentoRemoto(this IServiceCollection services, ProcessamentoRemotoOptions options, IAmazonSQS sqs)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sqs);
        options.Validar();
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ConsumidorSqs)))
        {
            throw new InvalidOperationException("O processamento remoto já foi configurado.");
        }
        services.AddSingleton(sqs);
        services.TryAddScoped<ObservabilidadeMensagens>();
        services.AddScoped<ITransporteMensagem>(_ => new TransporteSqs(sqs, options.FilaExecucaoUrl, options.FilaOrcamentoPagamentoUrl));
        services.AddScoped(provider => new ConsumidorSqs(sqs, provider.GetRequiredService<IProcessadorMensagem>(),
            options.FilaEntradaUrl, provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ObservabilidadeMensagens>()));
        services.AddScoped<PublicadorOutbox>();
        services.AddHostedService<ProcessamentoRemotoWorker>();
        return services;
    }
}
