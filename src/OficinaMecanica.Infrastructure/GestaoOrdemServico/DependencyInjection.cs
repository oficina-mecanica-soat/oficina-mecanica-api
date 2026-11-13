using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Repositories;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;

namespace OficinaMecanica.Infrastructure.GestaoOrdemServico;

public static class DependencyInjection
{
    public static IServiceCollection AddGestaoOrdemServicoInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IOrdemServicoRepository, OrdemServicoRepository>();
        services.AddScoped<ISagaIntegracaoRepository, SagaIntegracaoRepository>();
        services.AddScoped<IRecebimentoMensagemRepository, RecebimentoMensagemRepository>();
        services.AddScoped<ISnapshotOrcamentoRepository, SnapshotOrcamentoRepository>();

        return services;
    }
}
