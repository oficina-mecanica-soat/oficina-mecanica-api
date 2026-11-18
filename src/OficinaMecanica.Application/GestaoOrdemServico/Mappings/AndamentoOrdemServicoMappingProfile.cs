using AutoMapper;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.Responses;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;

namespace OficinaMecanica.Application.GestaoOrdemServico.Mappings;

public sealed class AndamentoOrdemServicoMappingProfile : Profile
{
    public AndamentoOrdemServicoMappingProfile()
    {
        CreateMap<SagaOrdemServico, AndamentoOrdemServicoResponse>()
            .ForCtorParam(nameof(AndamentoOrdemServicoResponse.SagaId), configuracao => configuracao.MapFrom(origem => origem.Id))
            .ForCtorParam(nameof(AndamentoOrdemServicoResponse.Estado), configuracao => configuracao.MapFrom(origem => origem.Estado.ToString()))
            .ForCtorParam(nameof(AndamentoOrdemServicoResponse.PagamentoConfirmado), configuracao => configuracao.MapFrom(origem => origem.Estado == EstadoSagaOrdemServico.PagamentoConfirmado));
    }
}
