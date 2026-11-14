using FluentValidation;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Application.GestaoOrdemServico.ValidationMessages;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;

public sealed class ItemSnapshotSagaValidator : AbstractValidator<ItemOrcamentoDados>
{
    public ItemSnapshotSagaValidator()
    {
        RuleFor(i => i.ItemId).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.ItemIdObrigatorio);
        RuleFor(i => i.Quantidade).GreaterThan(0).WithMessage(SagaIntegracaoValidationMessages.QuantidadeInvalida);
        RuleFor(i => i.Descricao).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.DescricaoInvalida)
            .MaximumLength(160).WithMessage(SagaIntegracaoValidationMessages.DescricaoInvalida);
        RuleFor(i => i.ValorUnitario).Must(v => v > 0 && v <= 9999999999999999.99m && decimal.Round(v, 2) == v)
            .WithMessage(SagaIntegracaoValidationMessages.ValorInvalido);
    }
}
