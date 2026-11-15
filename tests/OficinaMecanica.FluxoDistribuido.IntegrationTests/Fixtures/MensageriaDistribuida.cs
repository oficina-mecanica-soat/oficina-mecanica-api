using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoExecucao;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoOrcamento;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoPagamento;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoPreparacao;
using OficinaMecanica.Execucao.Application.Mensagens;
using OficinaMecanica.Execucao.Infrastructure.Persistencia;
using OficinaMecanica.Infrastructure.Persistence;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.ProcessarMensagemOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.ProcessarMensagemPagamento;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;
using OsMensageria = OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;
using OrcamentoMensageria = OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;
using ExecucaoMensageria = OficinaMecanica.Execucao.Infrastructure.Mensagens;

namespace OficinaMecanica.FluxoDistribuido.IntegrationTests.Fixtures;

internal sealed class MensageriaDistribuida(ServiceProvider os, ServiceProvider orcamento, ServiceProvider execucao,
    string diretorio, string correlacao, bool falharPublicacao)
{
    private readonly FalhasPublicacao? _falhas = falharPublicacao ? new() : null;
    private readonly RelogioPublicacao _relogio = new();
    public ContextoTraceJornada? ContextoTrace { get; set; }

    public void ConferirRetomada(params string[] origens) => _falhas?.ConferirRetomada(origens);

    public async Task ConvergirAsync()
    {
        for (var tentativa = 0; tentativa < 30; tentativa++)
        {
            var houveAtividade = await PublicarOsAsync();
            houveAtividade |= await PublicarOrcamentoAsync();
            houveAtividade |= await PublicarExecucaoAsync();

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
            if (!houveAtividade && _falhas?.HaReenvioPendente != true) return;
            // Avança apenas o relógio dos publicadores; não espera nem altera prazos de negócio.
            if (falharPublicacao) _relogio.Avancar();
        }
        throw new InvalidOperationException("Fluxo distribuído não convergiu no limite do teste.");
    }

    private static string Tipo(string corpo)
    {
        using var documento = JsonDocument.Parse(corpo);
        return documento.RootElement.GetProperty("tipo").GetString()!;
    }

    private async Task<bool> PublicarOsAsync()
    {
        await using var scope = os.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OficinaMecanicaDbContext>();
        var transporte = new TransporteComFalhaAposEnvio("OS", diretorio, _falhas);
        var publicou = await new OsMensageria.PublicadorOutbox(db, transporte, _relogio).PublicarProximaAsync();
        if (transporte.Interrompida is Guid id)
        {
            publicou.Should().BeFalse();
            var pendente = await db.Set<OsMensageria.RegistroOutboxSaga>().AsNoTracking().SingleAsync(r => r.Id == id);
            pendente.PublicadoEm.Should().BeNull();
            pendente.Tentativas.Should().Be(1);
            pendente.LeaseAte.Should().BeNull();
            pendente.Responsavel.Should().BeNull();
            pendente.ProximaTentativaEm.Should().BeAfter(_relogio.GetUtcNow());
        }
        return publicou;
    }

    private async Task<bool> PublicarOrcamentoAsync()
    {
        await using var scope = orcamento.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrcamentoPagamentoDbContext>();
        var transporte = new TransporteComFalhaAposEnvio("Orcamento", diretorio, _falhas);
        var publicou = await new OrcamentoMensageria.PublicadorOutbox(db, transporte, _relogio).PublicarProximaAsync();
        if (transporte.Interrompida is Guid id)
        {
            publicou.Should().BeFalse();
            var pendente = await db.Set<OrcamentoMensageria.RegistroOutbox>().AsNoTracking().SingleAsync(r => r.Id == id);
            pendente.PublicadoEm.Should().BeNull();
            pendente.Tentativas.Should().Be(1);
            pendente.LeaseAte.Should().BeNull();
            pendente.Responsavel.Should().BeNull();
            pendente.ProximaTentativaEm.Should().BeAfter(_relogio.GetUtcNow());
        }
        return publicou;
    }

    private async Task<bool> PublicarExecucaoAsync()
    {
        await using var scope = execucao.CreateAsyncScope();
        var armazenamento = scope.ServiceProvider.GetRequiredService<IArmazenamentoExecucao>();
        var transporte = new TransporteComFalhaAposEnvio("Execucao", diretorio, _falhas);
        var publicou = await new ExecucaoMensageria.PublicadorOutbox(armazenamento, transporte, _relogio).PublicarProximaAsync();
        if (transporte.Interrompida is Guid id)
        {
            publicou.Should().BeFalse();
            var registro = await armazenamento.ObterPrimeiraOutboxAsync(CancellationToken.None);
            registro.Should().NotBeNull();
            var pendente = registro!.Ler<PendenciaOutbox>()!;
            pendente.Mensagem.MensagemId.Should().Be(id);
            pendente.Tentativas.Should().Be(1);
            pendente.LeaseAte.Should().BeNull();
            pendente.Responsavel.Should().BeNull();
            pendente.ProximaTentativaEm.Should().BeAfter(_relogio.GetUtcNow());
        }
        return publicou;
    }

    private static async Task ReceberAsync(ServiceProvider servico, Func<IServiceProvider, Task> receber)
    {
        await using var scope = servico.CreateAsyncScope();
        await receber(scope.ServiceProvider);
    }

    private async Task<Dictionary<string, string>> CapturarOutboxesAsync()
    {
        var corpos = new Dictionary<string, string>();
        await using (var scope = os.CreateAsyncScope())
        {
            var registros = await scope.ServiceProvider.GetRequiredService<OficinaMecanicaDbContext>()
                .Set<OsMensageria.RegistroOutboxSaga>().AsNoTracking().ToListAsync();
            foreach (var registro in registros) corpos.Add("OS:" + registro.Id, registro.Corpo);
        }
        await using (var scope = orcamento.CreateAsyncScope())
        {
            var registros = await scope.ServiceProvider.GetRequiredService<OrcamentoPagamentoDbContext>()
                .Set<OrcamentoMensageria.RegistroOutbox>().AsNoTracking().ToListAsync();
            foreach (var registro in registros) corpos.Add("Orcamento:" + registro.Id, registro.Corpo);
        }
        await using (var scope = execucao.CreateAsyncScope())
        {
            var registro = await scope.ServiceProvider.GetRequiredService<IArmazenamentoExecucao>()
                .ObterPrimeiraOutboxAsync(CancellationToken.None);
            if (registro is not null)
            {
                var mensagem = registro.Ler<PendenciaOutbox>()!.Mensagem;
                corpos.Add("Execucao:" + mensagem.MensagemId, JsonContrato.Serializar(mensagem));
            }
        }
        return corpos;
    }

    private async Task<bool> TransferirAsync(string destino, Func<string, Task> receber)
    {
        var entrada = Path.Combine(diretorio, destino, "entrada");
        if (!Directory.Exists(entrada)) return false;
        var arquivos = Directory.GetFiles(entrada, "*.json").Order(StringComparer.Ordinal).ToArray();
        foreach (var arquivo in arquivos)
        {
            var corpo = await File.ReadAllTextAsync(arquivo);
            using var documento = JsonDocument.Parse(corpo);
            documento.RootElement.GetProperty("correlationId").GetString().Should().Be(correlacao);
            ContextoTrace?.Conferir(corpo);
            // Novo escopo em cada recebimento; o cenário de trace muda apenas metadados na reentrega.
            await receber(corpo);
            var anteriores = ContextoTrace is null ? null : await CapturarOutboxesAsync();
            await receber(ContextoTrace is null ? corpo : ContextoTraceJornada.AlterarReentrega(corpo));
            if (anteriores is not null)
                (await CapturarOutboxesAsync()).Should().BeEquivalentTo(anteriores,
                    "reentrega com outro trace não substitui o contexto ou o corpo persistido dos resultados");
            File.Delete(arquivo);
        }
        return arquivos.Length > 0;
    }
}
