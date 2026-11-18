using AutoMapper;
using FluentValidation;
using OficinaMecanica.Application.Common;
using OficinaMecanica.Domain.Atendimento.Interfaces;
using OficinaMecanica.Domain.Atendimento.ValueObjects;

namespace OficinaMecanica.Application.Atendimento.ClienteUseCases.ConsultarClienteAutenticacao;

public sealed class ConsultarClienteAutenticacaoUseCase
{
    private readonly IClienteRepository _clienteRepository;
    private readonly IValidator<ConsultarClienteAutenticacaoRequest> _validator;
    private readonly IMapper _mapper;

    public ConsultarClienteAutenticacaoUseCase(
        IClienteRepository clienteRepository,
        IValidator<ConsultarClienteAutenticacaoRequest> validator,
        IMapper mapper)
    {
        _clienteRepository = clienteRepository;
        _validator = validator;
        _mapper = mapper;
    }

    public async Task<Result<ConsultarClienteAutenticacaoResponse>> ExecuteAsync(ConsultarClienteAutenticacaoRequest request, CancellationToken cancellationToken = default)
    {
        var validacao = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacao.IsValid)
        {
            return Result<ConsultarClienteAutenticacaoResponse>.Falha(validacao.ObterMensagensErro(), TipoErro.Validacao);
        }
        var documento = CpfCnpj.Criar(request.Documento);
        var cliente = await _clienteRepository.ObterPorDocumentoAsync(documento.Numero, cancellationToken);
        return Result<ConsultarClienteAutenticacaoResponse>.Ok(cliente is null
            ? new ConsultarClienteAutenticacaoResponse(null, false)
            : _mapper.Map<ConsultarClienteAutenticacaoResponse>(cliente));
    }
}
