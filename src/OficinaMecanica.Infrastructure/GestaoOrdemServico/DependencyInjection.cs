using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Repositories;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Services;
using OficinaMecanica.Infrastructure.Observabilidade;

namespace OficinaMecanica.Infrastructure.GestaoOrdemServico;

public static class DependencyInjection
{
    public static IServiceCollection AddGestaoOrdemServicoInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IOrdemServicoRepository, OrdemServicoRepository>();
        services.AddScoped<ISagaOrdemServicoRepository, SagaOrdemServicoRepository>();
        services.TryAddScoped(provider => new ObservabilidadeMensagens(
            provider.GetService<ILogger<ObservabilidadeMensagens>>() ?? NullLogger<ObservabilidadeMensagens>.Instance));
        services.AddScoped<IOutbox, SqlOutbox>();
        services.AddScoped<ISnapshotOrcamentoStore, SnapshotOrcamentoStore>();
        services.AddScoped<IInbox, SqlInbox>();
        services.AddScoped<IConsultaSnapshotOrcamento, ConsultaSnapshotOrcamento>();

        return services;
    }
}
