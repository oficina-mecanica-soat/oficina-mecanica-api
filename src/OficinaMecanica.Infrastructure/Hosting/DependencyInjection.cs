using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;

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
        services.AddScoped<ITransporteMensagem>(_ => new TransporteSqs(sqs, options.FilaExecucaoUrl, options.FilaOrcamentoPagamentoUrl));
        services.AddScoped(provider => new ConsumidorSqs(sqs, provider.GetRequiredService<IProcessadorMensagem>(),
            options.FilaEntradaUrl, provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<PublicadorOutbox>();
        services.AddHostedService<ProcessamentoRemotoWorker>();
        return services;
    }
}
