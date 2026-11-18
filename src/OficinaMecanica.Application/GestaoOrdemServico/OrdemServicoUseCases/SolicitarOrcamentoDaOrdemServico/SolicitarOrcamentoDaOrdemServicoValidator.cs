using FluentValidation;
using OficinaMecanica.Application.GestaoOrdemServico.ValidationMessages;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoDaOrdemServico;

public sealed class SolicitarOrcamentoDaOrdemServicoValidator : AbstractValidator<SolicitarOrcamentoDaOrdemServicoRequest>
{
    public SolicitarOrcamentoDaOrdemServicoValidator()
    {
        RuleFor(r => r.SagaId).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.SagaObrigatoria);
        RuleFor(r => r.OrdemServicoId).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.OrdemObrigatoria);
        RuleFor(r => r.OperacaoId).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.OperacaoObrigatoria);
        RuleFor(r => r.CorrelationId).Must(v => !string.IsNullOrWhiteSpace(v) && v.Length <= 128 && !v.Any(char.IsControl))
            .WithMessage(SagaIntegracaoValidationMessages.CorrelacaoInvalida);
    }
}
