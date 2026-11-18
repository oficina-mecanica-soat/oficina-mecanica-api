using AutoMapper;
using FluentValidation;
using OficinaMecanica.Application.Common;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.Responses;
using OficinaMecanica.Domain.Atendimento.Interfaces;
using OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Domain.GestaoOrdemServico.Messages;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ConsultarAndamentoOrdemServico;

public sealed class ConsultarAndamentoOrdemServicoUseCase
{
    private readonly IOrdemServicoRepository _ordemServicoRepository;
    private readonly IVeiculoRepository _veiculoRepository;
    private readonly ISagaOrdemServicoRepository _sagaOrdemServicoRepository;
    private readonly IValidator<ConsultarAndamentoOrdemServicoRequest> _validator;
    private readonly IMapper _mapper;

    public ConsultarAndamentoOrdemServicoUseCase(
        IOrdemServicoRepository ordemServicoRepository,
        IVeiculoRepository veiculoRepository,
        ISagaOrdemServicoRepository sagaOrdemServicoRepository,
        IValidator<ConsultarAndamentoOrdemServicoRequest> validator,
        IMapper mapper)
    {
        _ordemServicoRepository = ordemServicoRepository;
        _veiculoRepository = veiculoRepository;
        _sagaOrdemServicoRepository = sagaOrdemServicoRepository;
        _validator = validator;
        _mapper = mapper;
    }

    public async Task<Result<AndamentoOrdemServicoResponse>> ExecuteAsync(ConsultarAndamentoOrdemServicoRequest request, CancellationToken cancellationToken = default)
    {
        var validacao = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacao.IsValid)
        {
            return Result<AndamentoOrdemServicoResponse>.Falha(validacao.ObterMensagensErro(), TipoErro.Validacao);
        }

        var ordemServico = await _ordemServicoRepository.ObterPorIdAsync(request.OrdemServicoId, cancellationToken);
        if (ordemServico is null)
        {
            return Result<AndamentoOrdemServicoResponse>.Falha(SagaOrdemServicoErrorMessages.AndamentoNaoEncontrado, TipoErro.NaoEncontrado);
        }
        if (request.ClienteId.HasValue)
        {
            var veiculo = await _veiculoRepository.ObterPorIdAsync(ordemServico.VeiculoId, cancellationToken);
            if (veiculo is null || veiculo.ClienteId != request.ClienteId)
            {
                return Result<AndamentoOrdemServicoResponse>.Falha(SagaOrdemServicoErrorMessages.AndamentoNaoEncontrado, TipoErro.NaoEncontrado);
            }
        }
        var saga = await _sagaOrdemServicoRepository.ObterPorOrdemAsync(ordemServico.Id, cancellationToken);
        if (saga is null)
        {
            return Result<AndamentoOrdemServicoResponse>.Falha(SagaOrdemServicoErrorMessages.AndamentoNaoEncontrado, TipoErro.NaoEncontrado);
        }
        return Result<AndamentoOrdemServicoResponse>.Ok(_mapper.Map<AndamentoOrdemServicoResponse>(saga));
    }
}
