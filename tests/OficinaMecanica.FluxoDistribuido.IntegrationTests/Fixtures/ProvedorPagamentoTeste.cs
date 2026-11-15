using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Contratos;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;

namespace OficinaMecanica.FluxoDistribuido.IntegrationTests.Fixtures;

internal sealed class ProvedorPagamentoTeste : IProvedorPagamento
{
    private CheckoutResponse? _checkout;
    public int QuantidadeCriacoes { get; private set; }
    public StatusPagamentoProvedor StatusConsulta { get; set; } = StatusPagamentoProvedor.Confirmado;
    public bool ConsultaIndisponivel { get; set; }
    public string MoedaConsulta { get; set; } = "BRL";
    public decimal DiferencaValor { get; set; }

    public Task<CheckoutResponse?> ConsultarCheckoutPorReferenciaAsync(Guid referencia, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_checkout?.Referencia == referencia ? _checkout : null);
    }

    public Task<CheckoutResponse> CriarCheckoutAsync(CriarCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        QuantidadeCriacoes++;
        _checkout = new("preferencia-local", request.Referencia, request.Valor, new Uri("https://sandbox.mercadopago.com.br/checkout/teste"));
        return Task.FromResult(_checkout);
    }

    public Task<PagamentoProvedorResponse> ConsultarPagamentoAsync(string pagamentoId, CancellationToken cancellationToken = default)
    {
        if (ConsultaIndisponivel) throw new HttpRequestException("Provedor sintético indisponível.");
        var checkout = _checkout ?? throw new InvalidOperationException("Checkout de teste ainda não criado.");
        return Task.FromResult(new PagamentoProvedorResponse(pagamentoId, checkout.Referencia,
            checkout.Valor + DiferencaValor, MoedaConsulta, StatusConsulta));
    }
}
