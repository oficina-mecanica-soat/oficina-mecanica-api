using FluentValidation;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.ValidationMessages;
namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;

public sealed class ServicoSnapshotSagaValidator : AbstractValidator<ServicoOrcamentoDados>
{
    public ServicoSnapshotSagaValidator()
    {
        RuleFor(i => i.ServicoId).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.ServicoIdObrigatorio);
        RuleFor(i => i.ServicoCatalogoId).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.ServicoCatalogoObrigatorio);
        RuleFor(i => i.Descricao).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.DescricaoInvalida)
            .MaximumLength(160).WithMessage(SagaIntegracaoValidationMessages.DescricaoInvalida);
        RuleFor(i => i.Valor).Must(v => v > 0 && v <= 9999999999999999.99m && decimal.Round(v, 2) == v)
            .WithMessage(SagaIntegracaoValidationMessages.ValorInvalido);
    }
}
