using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Repositories;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Services;

namespace OficinaMecanica.Infrastructure.GestaoOrdemServico;

public static class DependencyInjection
{
    public static IServiceCollection AddGestaoOrdemServicoInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IOrdemServicoRepository, OrdemServicoRepository>();
        services.AddScoped<ISagaOrdemServicoRepository, SagaOrdemServicoRepository>();
        services.AddScoped<IOutbox, SqlOutbox>();
        services.AddScoped<ISnapshotOrcamentoStore, SnapshotOrcamentoStore>();
        services.AddScoped<IInbox, SqlInbox>();
        services.AddScoped<IConsultaSnapshotOrcamento, ConsultaSnapshotOrcamento>();

        return services;
    }
}
