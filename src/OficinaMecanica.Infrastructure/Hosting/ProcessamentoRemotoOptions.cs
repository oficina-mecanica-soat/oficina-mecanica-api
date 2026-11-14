using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;

namespace OficinaMecanica.Infrastructure.Hosting;

public sealed record ProcessamentoRemotoOptions(string FilaEntradaUrl, string FilaExecucaoUrl, string FilaOrcamentoPagamentoUrl)
{
    public void Validar()
    {
        ContratoFilaSqs.ValidarUrl(FilaEntradaUrl);
        ContratoFilaSqs.ValidarUrl(FilaExecucaoUrl);
        ContratoFilaSqs.ValidarUrl(FilaOrcamentoPagamentoUrl);
        var filas = new[] { new Uri(FilaEntradaUrl), new Uri(FilaExecucaoUrl), new Uri(FilaOrcamentoPagamentoUrl) };
        if (filas.Distinct().Count() != filas.Length)
        {
            throw new ArgumentException("As filas de OS, Execução e Orçamento/Pagamento devem ser distintas.");
        }
    }
}
