using FluentValidation;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;
using OficinaMecanica.Application.GestaoOrdemServico.ValidationMessages;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;

public sealed class SolicitarOrcamentoSagaValidator : AbstractValidator<SolicitarOrcamentoSagaRequest>
{
    public SolicitarOrcamentoSagaValidator()
    {
        RuleFor(r => r.SagaId).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.SagaObrigatoria);
        RuleFor(r => r.OrdemServicoId).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.OrdemObrigatoria);
        RuleFor(r => r.OperacaoId).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.OperacaoObrigatoria);
        RuleFor(r => r.CorrelationId).Must(v => !string.IsNullOrWhiteSpace(v) && v.Length <= 128 && !v.Any(char.IsControl)).WithMessage(SagaIntegracaoValidationMessages.CorrelacaoInvalida);
        RuleFor(r => r.Dados).NotNull().WithMessage(SagaIntegracaoValidationMessages.SnapshotObrigatorio);
        When(r => r.Dados is not null, () =>
        {
            RuleFor(r => r.Dados).Must(TotalValido).WithMessage(SagaIntegracaoValidationMessages.TotalInvalido);
            RuleFor(r => r.Dados.ClienteId).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.ClienteObrigatorio);
            RuleFor(r => r.Dados.Versao).GreaterThan(0).WithMessage(SagaIntegracaoValidationMessages.VersaoInvalida);
            RuleFor(r => r.Dados.Servicos).NotEmpty().WithMessage(SagaIntegracaoValidationMessages.ServicosObrigatorios);
            RuleFor(r => r.Dados.Itens).NotNull().WithMessage(SagaIntegracaoValidationMessages.ItensObrigatorios);
            RuleForEach(r => r.Dados.Servicos).NotNull().WithMessage(SagaIntegracaoValidationMessages.ServicosInvalidos);
            RuleForEach(r => r.Dados.Itens).NotNull().WithMessage(SagaIntegracaoValidationMessages.ItensInvalidos);
            RuleForEach(r => r.Dados.Servicos).Where(s => s is not null).SetValidator(new ServicoSnapshotSagaValidator());
            RuleForEach(r => r.Dados.Itens).Where(i => i is not null).SetValidator(new ItemSnapshotSagaValidator());
            RuleFor(r => r.Dados.Servicos).Must(servicos => servicos is null || servicos.Where(s => s is not null).Select(s => s.ServicoId).Distinct().Count() == servicos.Length)
                .WithMessage(SagaIntegracaoValidationMessages.ServicosInvalidos);
        });
    }

    private static bool TotalValido(GerarOrcamentoDados dados)
    {
        const decimal maximo = 9999999999999999.99m;
        if (dados.Servicos is null || dados.Itens is null)
        {
            return false;
        }
        decimal total = 0;
        foreach (var servico in dados.Servicos)
        {
            if (servico is null || servico.Valor <= 0 || servico.Valor > maximo - total)
            {
                return false;
            }
            total += servico.Valor;
        }
        foreach (var item in dados.Itens)
        {
            if (item is null || item.Quantidade <= 0 || item.ValorUnitario <= 0 || item.ValorUnitario > maximo / item.Quantidade)
            {
                return false;
            }
            var valor = item.ValorUnitario * item.Quantidade;
            if (valor > maximo - total)
            {
                return false;
            }
            total += valor;
        }
        return total > 0;
    }
}
