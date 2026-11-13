using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases;
using FluentValidation;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;

namespace OficinaMecanica.Application.GestaoOrdemServico;

public static class DependencyInjection
{
    public static IServiceCollection AddGestaoOrdemServicoApplication(this IServiceCollection services)
    {
        services.AddOrdemServicoUseCases();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ProcessadorResultadoOrcamento>();
        services.AddScoped<SolicitarOrcamentoSagaUseCase>();
        services.AddScoped<IValidator<SolicitarOrcamentoSagaRequest>, SolicitarOrcamentoSagaValidator>();

        return services;
    }
}
