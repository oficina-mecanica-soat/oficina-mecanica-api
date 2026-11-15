using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoExecucao;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoOrcamento;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoPagamento;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoPreparacao;
using OficinaMecanica.Execucao.Application.Mensagens;
using OficinaMecanica.Infrastructure.Persistence;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.ProcessarMensagemOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.ProcessarMensagemPagamento;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;
using OsMensageria = OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;
using OrcamentoMensageria = OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
using ExecucaoMensageria = OficinaMecanica.Execucao.Infrastructure.Mensagens;

namespace OficinaMecanica.FluxoDistribuido.IntegrationTests.Fixtures;

internal sealed class MensageriaDistribuida(ServiceProvider os, ServiceProvider orcamento, ServiceProvider execucao, string diretorio)
{
    public async Task ConvergirAsync()
    {
        for (var tentativa = 0; tentativa < 30; tentativa++)
        {
            var houveAtividade = await PublicarAsync(os, async provider =>
                await new OsMensageria.PublicadorOutbox(provider.GetRequiredService<OficinaMecanicaDbContext>(),
                    new OsMensageria.TransporteArquivos(diretorio), TimeProvider.System).PublicarProximaAsync());
            houveAtividade |= await PublicarAsync(orcamento, async provider =>
                await new OrcamentoMensageria.PublicadorOutbox(provider.GetRequiredService<OrcamentoPagamentoDbContext>(),
                    new OrcamentoMensageria.TransporteArquivos(diretorio), TimeProvider.System).PublicarProximaAsync());
            houveAtividade |= await PublicarAsync(execucao, async provider =>
                await provider.GetRequiredService<ExecucaoMensageria.PublicadorOutbox>().PublicarProximaAsync());

            houveAtividade |= await TransferirAsync("Execucao", corpo => ReceberAsync(execucao, async provider =>
                await provider.GetRequiredService<ProcessadorPreparacao>().ProcessarAsync(corpo)));
            houveAtividade |= await TransferirAsync("OrcamentoPagamento", corpo => ReceberAsync(orcamento, async provider =>
            {
                if (Tipo(corpo) == "CriarCobranca")
                    await provider.GetRequiredService<ProcessarMensagemPagamentoUseCase>().ExecuteAsync(corpo);
                else
                    await provider.GetRequiredService<ProcessarMensagemOrcamentoUseCase>().ExecuteAsync(corpo);
            }));
            houveAtividade |= await TransferirAsync("OrdemServico", corpo => ReceberAsync(os, async provider =>
            {
                switch (Tipo(corpo))
                {
                    case "ExecucaoPreparada":
                    case "PreparacaoRecusada":
                        await provider.GetRequiredService<ProcessarResultadoPreparacaoUseCase>().ExecuteAsync(corpo);
                        break;
                    case "OrcamentoGerado":
                    case "OrcamentoDecidido":
                    case "OrcamentoCancelado":
                    case "PreparacaoCancelada":
                        await provider.GetRequiredService<ProcessarResultadoOrcamentoUseCase>().ExecuteAsync(corpo);
                        break;
                    case "ExecucaoIniciada":
                    case "ExecucaoFinalizada":
                        await provider.GetRequiredService<ProcessarResultadoExecucaoUseCase>().ExecuteAsync(corpo);
                        break;
                    case "CobrancaCriada":
                    case "PagamentoConfirmado":
                        await provider.GetRequiredService<ProcessarResultadoPagamentoUseCase>().ExecuteAsync(corpo);
                        break;
                    default:
                        throw new InvalidOperationException("Resultado sem consumidor no roteiro distribuído.");
                }
            }));
            if (!houveAtividade) return;
        }
        throw new InvalidOperationException("Fluxo distribuído não convergiu no limite do teste.");
    }

    private static string Tipo(string corpo)
    {
        using var documento = JsonDocument.Parse(corpo);
        return documento.RootElement.GetProperty("tipo").GetString()!;
    }

    private static async Task<bool> PublicarAsync(ServiceProvider servico, Func<IServiceProvider, Task<bool>> publicar)
    {
        await using var scope = servico.CreateAsyncScope();
        return await publicar(scope.ServiceProvider);
    }

    private static async Task ReceberAsync(ServiceProvider servico, Func<IServiceProvider, Task> receber)
    {
        await using var scope = servico.CreateAsyncScope();
        await receber(scope.ServiceProvider);
    }

    private async Task<bool> TransferirAsync(string destino, Func<string, Task> receber)
    {
        var entrada = Path.Combine(diretorio, destino, "entrada");
        if (!Directory.Exists(entrada)) return false;
        var arquivos = Directory.GetFiles(entrada, "*.json").Order(StringComparer.Ordinal).ToArray();
        foreach (var arquivo in arquivos)
        {
            var corpo = await File.ReadAllTextAsync(arquivo);
            // Cada recebimento usa um novo escopo; repetir o JSON testa a Inbox persistida.
            await receber(corpo);
            await receber(corpo);
            File.Delete(arquivo);
        }
        return arquivos.Length > 0;
    }
}
