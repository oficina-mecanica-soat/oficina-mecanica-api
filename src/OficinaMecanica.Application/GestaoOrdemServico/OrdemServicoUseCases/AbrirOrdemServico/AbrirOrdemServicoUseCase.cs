using AutoMapper;
using FluentValidation;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Application.Common;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarPreparacaoOrdemServico;
using OficinaMecanica.Application.GestaoOrdemServico.ValidationMessages;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ReservarPecaInsumo;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.Responses;
using OficinaMecanica.Domain.Administrativo.Aggregates;
using OficinaMecanica.Domain.Administrativo.Interfaces;
using OficinaMecanica.Domain.Administrativo.Messages;
using OficinaMecanica.Domain.Atendimento.Aggregates;
using OficinaMecanica.Domain.Atendimento.Interfaces;
using OficinaMecanica.Domain.Atendimento.Messages;
using OficinaMecanica.Domain.Atendimento.ValueObjects;
using OficinaMecanica.Domain.GestaoEstoque.Aggregates;
using OficinaMecanica.Domain.GestaoEstoque.Interfaces;
using OficinaMecanica.Domain.GestaoEstoque.Messages;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.AbrirOrdemServico;

public sealed class AbrirOrdemServicoUseCase
{
    private readonly IOrdemServicoRepository _ordemServicoRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly IVeiculoRepository _veiculoRepository;
    private readonly IMecanicoRepository _mecanicoRepository;
    private readonly IServicoCatalogoRepository _servicoCatalogoRepository;
    private readonly IPecaInsumoCatalogoRepository _pecaInsumoCatalogoRepository;
    private readonly IEstoqueRepository _estoqueRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly SolicitarPreparacaoOrdemServicoUseCase _solicitarPreparacaoUseCase;
    private readonly IValidator<AbrirOrdemServicoRequest> _validator;
    private readonly IMapper _mapper;
    private readonly ILogger<AbrirOrdemServicoUseCase> _logger;

    public AbrirOrdemServicoUseCase(
        AbrirOrdemServicoRepositorios repositorios,
        IUnitOfWork unitOfWork,
        IValidator<AbrirOrdemServicoRequest> validator,
        IMapper mapper,
        SolicitarPreparacaoOrdemServicoUseCase solicitarPreparacaoUseCase,
        ILogger<AbrirOrdemServicoUseCase> logger)
    {
        _ordemServicoRepository = repositorios.OrdemServico;
        _clienteRepository = repositorios.Cliente;
        _veiculoRepository = repositorios.Veiculo;
        _mecanicoRepository = repositorios.Mecanico;
        _servicoCatalogoRepository = repositorios.ServicoCatalogo;
        _pecaInsumoCatalogoRepository = repositorios.PecaInsumoCatalogo;
        _estoqueRepository = repositorios.Estoque;
        _unitOfWork = unitOfWork;
        _solicitarPreparacaoUseCase = solicitarPreparacaoUseCase;
        _validator = validator;
        _mapper = mapper;
        _logger = logger;
    }

    public Task<Result<OrdemServicoResponse>> ExecuteAsync(AbrirOrdemServicoRequest request, CancellationToken cancellationToken = default)
    {
        return ExecutarAsync(request, false, null, cancellationToken);
    }

    public Task<Result<OrdemServicoResponse>> ExecuteDistribuidoAsync(AbrirOrdemServicoRequest request, CancellationToken cancellationToken = default)
    {
        return ExecutarAsync(request, true, null, cancellationToken);
    }

    public Task<Result<OrdemServicoResponse>> ExecuteDistribuidoAsync(AbrirOrdemServicoRequest request, string? correlationId, CancellationToken cancellationToken)
    {
        return ExecutarAsync(request, true, correlationId, cancellationToken);
    }

    private async Task<Result<OrdemServicoResponse>> ExecutarAsync(AbrirOrdemServicoRequest request, bool distribuido, string? correlationId, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            return Result<OrdemServicoResponse>.Falha(validationResult.ObterMensagensErro(), TipoErro.Validacao);
        }

        if (distribuido && (!request.FilialId.HasValue || request.FilialId == Guid.Empty))
        {
            return Result<OrdemServicoResponse>.Falha(SagaIntegracaoValidationMessages.FilialObrigatoria, TipoErro.Validacao);
        }

        var cliente = await ObterClienteAsync(request, cancellationToken);
        if (cliente is null)
        {
            return Result<OrdemServicoResponse>.Falha(ClienteErrorMessages.ClienteNaoEncontrado, TipoErro.NaoEncontrado);
        }

        var veiculo = await _veiculoRepository.ObterPorIdAsync(request.VeiculoId, cancellationToken);
        if (veiculo is null || veiculo.ClienteId != cliente.Id)
        {
            return Result<OrdemServicoResponse>.Falha(VeiculoErrorMessages.VeiculoNaoEncontrado, TipoErro.NaoEncontrado);
        }

        var mecanico = await _mecanicoRepository.ObterPorIdAsync(request.MecanicoId, cancellationToken);
        if (mecanico is null)
        {
            return Result<OrdemServicoResponse>.Falha(MecanicoErrorMessages.MecanicoNaoEncontrado, TipoErro.NaoEncontrado);
        }

        var numero = await _ordemServicoRepository.ObterProximoNumeroAsync(cancellationToken);
        var ordemServico = OrdemServico.Abrir(numero, request.VeiculoId, request.MecanicoId);
        var servicosCatalogoIds = request.ServicosCatalogoIds ?? [];
        var pecasInsumos = request.PecasInsumos ?? [];

        var servicosCatalogo = await ObterServicosCatalogoAsync(servicosCatalogoIds, cancellationToken);
        if (servicosCatalogoIds.Any(servicoCatalogoId => !servicosCatalogo.ContainsKey(servicoCatalogoId)))
        {
            return Result<OrdemServicoResponse>.Falha(ServicoCatalogoErrorMessages.ServicoCatalogoNaoEncontrado, TipoErro.NaoEncontrado);
        }

        foreach (var servicoCatalogoId in servicosCatalogoIds)
        {
            var servicoCatalogo = servicosCatalogo[servicoCatalogoId];

            ordemServico.RegistrarServicoNaAbertura(servicoCatalogo.Id, servicoCatalogo.Valor);
        }

        var resultadoReserva = await ReservarPecasInsumosAsync(ordemServico, pecasInsumos, !distribuido, cancellationToken);
        if (!resultadoReserva.Sucesso)
        {
            if (IsStockProcessingFailure(resultadoReserva.Erro!.Mensagem))
            {
                _logger.LogWarning(
                    "Falha ao processar a abertura da ordem de servico {OrdemServicoId}: {failure_reason}. {operation} {bounded_context}",
                    ordemServico.Id,
                    resultadoReserva.Erro.Mensagem,
                    "AbrirOrdemServico",
                    "GestaoOrdemServico");
            }

            return Result<OrdemServicoResponse>.Falha(resultadoReserva.Erro!.Mensagem, resultadoReserva.Erro.Tipo);
        }

        var estoque = resultadoReserva.Valor;

        if (distribuido)
        {
            Result<Guid>? preparacao = null;
            var operacaoId = Guid.CreateVersion7();
            var correlacao = correlationId ?? Guid.CreateVersion7().ToString();
            try
            {
                await _unitOfWork.ExecutarEmTransacaoAsync(async transactionCancellationToken =>
                {
                    await _ordemServicoRepository.AdicionarAsync(ordemServico, transactionCancellationToken);
                    preparacao = await _solicitarPreparacaoUseCase.ExecuteAsync(
                        new(ordemServico.Id, request.FilialId!.Value, operacaoId, correlacao), transactionCancellationToken);
                    if (!preparacao.Sucesso)
                    {
                        throw new PreparacaoNaoIniciadaException();
                    }
                }, cancellationToken);
            }
            catch (PreparacaoNaoIniciadaException)
            {
                var erro = preparacao!.Erro!;
                return Result<OrdemServicoResponse>.Falha(erro.Mensagem, erro.Tipo);
            }
        }
        else if (estoque is null)
        {
            await _ordemServicoRepository.AdicionarAsync(ordemServico, cancellationToken);
        }
        else
        {
            await _unitOfWork.ExecutarEmTransacaoAsync(
                async transactionCancellationToken =>
                {
                    await _ordemServicoRepository.AdicionarAsync(ordemServico, transactionCancellationToken);
                    await _estoqueRepository.AtualizarAsync(estoque, transactionCancellationToken);
                },
                cancellationToken);
        }

        _logger.LogInformation(
            "Ordem de servico {OrdemServicoId} aberta com numero {OrdemServicoNumero} e status {OrdemServicoStatus}. {operation} {bounded_context}",
            ordemServico.Id,
            ordemServico.Numero,
            ordemServico.Status,
            "AbrirOrdemServico",
            "GestaoOrdemServico");

        return Result<OrdemServicoResponse>.Ok(_mapper.Map<OrdemServicoResponse>(ordemServico));
    }

    private async Task<Result<Estoque?>> ReservarPecasInsumosAsync(OrdemServico ordemServico, IReadOnlyCollection<PecaInsumoRequest> pecasInsumos, bool reservarEstoque, CancellationToken cancellationToken)
    {
        if (pecasInsumos.Count == 0)
        {
            return Result<Estoque?>.Ok(null);
        }

        var estoque = reservarEstoque ? await _estoqueRepository.ObterAsync(cancellationToken) : null;
        if (reservarEstoque && estoque is null)
        {
            return Result<Estoque?>.Falha(EstoqueErrorMessages.EstoqueNaoEncontrado, TipoErro.NaoEncontrado);
        }

        var pecasInsumosCatalogo = await ObterPecasInsumosCatalogoAsync(pecasInsumos, cancellationToken);
        if (pecasInsumos.Any(pecaInsumo => !pecasInsumosCatalogo.ContainsKey(pecaInsumo.PecaInsumoCatalogoId)))
        {
            return Result<Estoque?>.Falha(PecaInsumoCatalogoErrorMessages.PecaInsumoCatalogoNaoEncontrado, TipoErro.NaoEncontrado);
        }

        if (reservarEstoque && !ExisteEstoqueDisponivel(estoque!, pecasInsumos))
        {
            return Result<Estoque?>.Falha(EstoqueErrorMessages.EstoqueInsuficiente, TipoErro.RegraNegocio);
        }

        foreach (var pecaInsumo in pecasInsumos)
        {
            var pecaInsumoCatalogo = pecasInsumosCatalogo[pecaInsumo.PecaInsumoCatalogoId];

            ordemServico.RegistrarPecaInsumoNaAbertura(pecaInsumoCatalogo.Id, pecaInsumo.Quantidade, pecaInsumoCatalogo.Valor);
            estoque?.ReservarItens(pecaInsumoCatalogo.Id, pecaInsumo.Quantidade);
        }

        return Result<Estoque?>.Ok(estoque);
    }

    private async Task<Cliente?> ObterClienteAsync(AbrirOrdemServicoRequest request, CancellationToken cancellationToken)
    {
        if (request.ClienteId is Guid clienteId && clienteId != Guid.Empty)
        {
            return await _clienteRepository.ObterPorIdAsync(clienteId, cancellationToken);
        }

        var documento = CpfCnpj.Criar(request.DocumentoCliente!);

        return await _clienteRepository.ObterPorDocumentoAsync(documento.Numero, cancellationToken);
    }

    private async Task<Dictionary<Guid, ServicoCatalogo>> ObterServicosCatalogoAsync(IReadOnlyCollection<Guid> servicosCatalogoIds, CancellationToken cancellationToken)
    {
        if (servicosCatalogoIds.Count == 0)
        {
            return [];
        }

        var ids = servicosCatalogoIds
            .Distinct()
            .ToArray();

        var servicosCatalogo = await _servicoCatalogoRepository.ObterPorIdsAsync(ids, cancellationToken);

        return servicosCatalogo.ToDictionary(servicoCatalogo => servicoCatalogo.Id);
    }

    private async Task<Dictionary<Guid, PecaInsumoCatalogo>> ObterPecasInsumosCatalogoAsync(IEnumerable<PecaInsumoRequest> pecasInsumos, CancellationToken cancellationToken)
    {
        var ids = pecasInsumos
            .Select(pecaInsumo => pecaInsumo.PecaInsumoCatalogoId)
            .Distinct()
            .ToArray();

        var pecasInsumosCatalogo = await _pecaInsumoCatalogoRepository.ObterPorIdsAsync(ids, cancellationToken);

        return pecasInsumosCatalogo.ToDictionary(pecaInsumoCatalogo => pecaInsumoCatalogo.Id);
    }

    private static bool ExisteEstoqueDisponivel(Estoque estoque, IEnumerable<PecaInsumoRequest> pecasInsumos)
    {
        return pecasInsumos.All(pecaInsumo => estoque.VerificarDisponibilidade(pecaInsumo.PecaInsumoCatalogoId, pecaInsumo.Quantidade));
    }

    private static bool IsStockProcessingFailure(string failureReason)
    {
        return failureReason == EstoqueErrorMessages.EstoqueNaoEncontrado
            || failureReason == EstoqueErrorMessages.EstoqueInsuficiente;
    }

    private sealed class PreparacaoNaoIniciadaException : Exception
    {
    }
}
