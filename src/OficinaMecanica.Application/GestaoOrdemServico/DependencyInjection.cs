using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases;
using FluentValidation;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoDaOrdemServico;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarPreparacaoOrdemServico;

namespace OficinaMecanica.Application.GestaoOrdemServico;

public static class DependencyInjection
{
    public static IServiceCollection AddGestaoOrdemServicoApplication(this IServiceCollection services)
    {
        services.AddOrdemServicoUseCases();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ProcessadorResultadoOrcamento>();
        services.AddScoped<ProcessadorResultadoPreparacao>();
        services.AddScoped<SolicitarPreparacaoOrdemServicoUseCase>();
        services.AddScoped<IValidator<SolicitarPreparacaoOrdemServicoRequest>, SolicitarPreparacaoOrdemServicoValidator>();
        services.AddScoped<SolicitarOrcamentoSagaUseCase>();
        services.AddScoped<IValidator<SolicitarOrcamentoSagaRequest>, SolicitarOrcamentoSagaValidator>();
        services.AddScoped<SolicitarOrcamentoDaOrdemServicoUseCase>();
        services.AddScoped<IValidator<SolicitarOrcamentoDaOrdemServicoRequest>, SolicitarOrcamentoDaOrdemServicoValidator>();

        return services;
    }
}
