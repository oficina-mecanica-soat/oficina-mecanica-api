using System.Text.Json;
using OficinaMecanica.Execucao.Application.Mensagens;
using OsMensageria = OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;
using OrcamentoMensageria = OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
using ExecucaoMensageria = OficinaMecanica.Execucao.Infrastructure.Mensagens;

namespace OficinaMecanica.FluxoDistribuido.IntegrationTests.Fixtures;

internal sealed class TransporteComFalhaAposEnvio(string origem, string diretorio, FalhasPublicacao? falhas) :
    OficinaMecanica.Application.GestaoOrdemServico.Interfaces.ITransporteMensagem,
    OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces.ITransporteMensagem,
    ITransporteResultado
{
    public Guid? Interrompida { get; private set; }

    public async Task PublicarAsync(string destino, string corpo, CancellationToken cancellationToken)
    {
        if (origem == "OS")
            await new OsMensageria.TransporteArquivos(diretorio).PublicarAsync(destino, corpo, cancellationToken);
        else if (origem == "Orcamento")
            await new OrcamentoMensageria.TransporteArquivos(diretorio).PublicarAsync(destino, corpo, cancellationToken);
        else
            throw new InvalidOperationException("Origem SQL não prevista na fixture.");
        Interromper(corpo);
    }

    public async Task PublicarAsync(Mensagem mensagem, CancellationToken cancellationToken)
    {
        await new ExecucaoMensageria.TransporteArquivos(Path.Combine(diretorio, "OrdemServico", "entrada"))
            .PublicarAsync(mensagem, cancellationToken);
        Interromper(JsonContrato.Serializar(mensagem));
    }

    private void Interromper(string corpo)
    {
        if (falhas?.Registrar(origem, corpo) != true) return;
        using var documento = JsonDocument.Parse(corpo);
        Interrompida = documento.RootElement.GetProperty("mensagemId").GetGuid();
        throw new IOException("Falha de teste após transporte e antes da confirmação da Outbox.");
    }
}
