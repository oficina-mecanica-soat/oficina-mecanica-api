using AutoMapper;
using OficinaMecanica.Application.Atendimento.ClienteUseCases.ConsultarClienteAutenticacao;
using OficinaMecanica.Application.Atendimento.ClienteUseCases.Responses;
using OficinaMecanica.Domain.Atendimento.Aggregates;
using OficinaMecanica.Domain.Atendimento.Enums;
using OficinaMecanica.Domain.Atendimento.ValueObjects;

namespace OficinaMecanica.Application.Atendimento.Mappings;

public sealed class ClienteMappingProfile : Profile
{
    public ClienteMappingProfile()
    {
        CreateMap<Endereco, EnderecoResponse>();

        CreateMap<Cliente, ClienteResponse>()
            .ForCtorParam(nameof(ClienteResponse.Documento), opcao => opcao.MapFrom(origem => origem.Documento.Numero))
            .ForCtorParam(nameof(ClienteResponse.Telefone), opcao => opcao.MapFrom(origem => origem.Telefone.Numero))
            .ForCtorParam(nameof(ClienteResponse.Email), opcao => opcao.MapFrom(origem => origem.Email.Endereco));

        CreateMap<Cliente, ConsultarClienteAutenticacaoResponse>()
            .ForCtorParam(nameof(ConsultarClienteAutenticacaoResponse.ClienteId), opcao => opcao.MapFrom(origem => origem.Id))
            .ForCtorParam(nameof(ConsultarClienteAutenticacaoResponse.Ativo), opcao => opcao.MapFrom(origem => origem.Status == StatusCliente.Ativo));
    }
}

