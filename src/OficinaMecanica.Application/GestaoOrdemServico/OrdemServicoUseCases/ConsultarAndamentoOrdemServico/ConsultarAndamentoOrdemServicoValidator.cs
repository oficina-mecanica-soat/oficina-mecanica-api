using FluentValidation;
using OficinaMecanica.Application.GestaoOrdemServico.ValidationMessages;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ConsultarAndamentoOrdemServico;

public sealed class ConsultarAndamentoOrdemServicoValidator : AbstractValidator<ConsultarAndamentoOrdemServicoRequest>
{
    public ConsultarAndamentoOrdemServicoValidator()
    {
        RuleFor(request => request).NotNull().WithMessage(OrdemServicoValidationMessages.RequestConsultarAndamentoObrigatorio);
        When(request => request is not null, () =>
        {
            RuleFor(request => request.OrdemServicoId).NotEmpty().WithMessage(OrdemServicoValidationMessages.OrdemServicoIdObrigatorio);
            RuleFor(request => request.ClienteId).NotEqual(Guid.Empty).When(request => request.ClienteId.HasValue)
                .WithMessage(OrdemServicoValidationMessages.ClienteIdInvalido);
        });
    }
}
