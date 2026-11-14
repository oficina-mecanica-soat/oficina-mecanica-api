using FluentValidation;
using OficinaMecanica.Application.Atendimento.ValidationMessages;
using OficinaMecanica.Application.Common;

namespace OficinaMecanica.Application.Atendimento.ClienteUseCases.ConsultarClienteAutenticacao;

public sealed class ConsultarClienteAutenticacaoValidator : AbstractValidator<ConsultarClienteAutenticacaoRequest>
{
    public ConsultarClienteAutenticacaoValidator()
    {
        RuleFor(request => request).NotNull().WithMessage(ValidationErrorMessages.RequestInvalido);
        When(request => request is not null, () =>
        {
            RuleFor(request => request.Documento).NotEmpty().WithMessage(ClienteValidationMessages.DocumentoObrigatorio);
        });
    }
}
