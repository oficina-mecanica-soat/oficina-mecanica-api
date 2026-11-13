using FluentValidation;
using OficinaMecanica.Application.GestaoOrdemServico.ValidationMessages;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarPreparacaoOrdemServico;

public sealed class SolicitarPreparacaoOrdemServicoValidator : AbstractValidator<SolicitarPreparacaoOrdemServicoRequest>
{
    public SolicitarPreparacaoOrdemServicoValidator()
    {
        RuleFor(r => r.OrdemServicoId).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.OrdemObrigatoria);
        RuleFor(r => r.FilialId).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.FilialObrigatoria);
        RuleFor(r => r.OperacaoId).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.OperacaoObrigatoria);
        RuleFor(r => r.CorrelationId).Must(v => !string.IsNullOrWhiteSpace(v) && v.Length <= 128 && !v.Any(char.IsControl))
            .WithMessage(SagaIntegracaoValidationMessages.CorrelacaoInvalida);
    }
}
