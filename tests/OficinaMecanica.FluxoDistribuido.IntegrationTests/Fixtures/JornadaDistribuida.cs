using Amazon.DynamoDBv2;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.AbrirOrdemServico;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ConsultarAndamentoOrdemServico;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.EntregarOrdemServico;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.Responses;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Execucao.Application.Preparacoes.FinalizarExecucao;
using OficinaMecanica.Execucao.Application.Preparacoes.FinalizarServico;
using OficinaMecanica.Execucao.Application.Preparacoes.IniciarExecucaoServico;
using OficinaMecanica.Execucao.Domain.GestaoEstoque.Entities;
using OficinaMecanica.Execucao.Infrastructure.Persistencia;
using OficinaMecanica.FluxoDistribuido.IntegrationTests.Factories;
using OficinaMecanica.Infrastructure.Persistence;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.RegistrarDecisaoOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.ConferirPagamento;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.FluxoDistribuido.IntegrationTests.Fixtures;

internal sealed class JornadaDistribuida : IAsyncDisposable
{
    private readonly ServiceProvider _os;
    private readonly ServiceProvider _orcamento;
    private readonly ServiceProvider _execucao;
    private readonly AmazonDynamoDBClient _clienteDynamo;
    private readonly string _diretorio;
    private readonly MensageriaDistribuida _mensageria;
    private readonly DadosJornada _dados = DadosJornadaFactory.Criar();
    private readonly Guid _operacaoInicio = Guid.NewGuid();
    private readonly Guid _operacaoFimServico = Guid.NewGuid();
    private readonly Guid _operacaoFimExecucao = Guid.NewGuid();
    private readonly Guid _operacaoDecisao = Guid.NewGuid();

    public OrdemServicoResponse Ordem { get; private set; } = null!;
    public ProvedorPagamentoTeste Provedor { get; }

    public JornadaDistribuida(ServiceProvider os, ServiceProvider orcamento, ServiceProvider execucao,
        AmazonDynamoDBClient clienteDynamo, string diretorio, ProvedorPagamentoTeste provedor)
    {
        _os = os;
        _orcamento = orcamento;
        _execucao = execucao;
        _clienteDynamo = clienteDynamo;
        _diretorio = diretorio;
        Provedor = provedor;
        _mensageria = new(os, orcamento, execucao, diretorio);
    }

    public async Task AbrirAsync(int quantidade = 3)
    {
        await using (var scope = _os.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OficinaMecanicaDbContext>();
            db.AddRange(_dados.Cliente, _dados.Veiculo, _dados.Mecanico, _dados.Servico, _dados.Peca);
            await db.SaveChangesAsync();
            var resultado = await scope.ServiceProvider.GetRequiredService<AbrirOrdemServicoUseCase>().ExecuteDistribuidoAsync(
                new(_dados.Cliente.Id, null, _dados.Veiculo.Id, _dados.Mecanico.Id,
                    [_dados.Servico.Id], [new(_dados.Peca.Id, quantidade)], _dados.FilialId));
            resultado.Sucesso.Should().BeTrue();
            Ordem = resultado.Valor!;
        }
        await using var execucao = _execucao.CreateAsyncScope();
        var armazenamento = execucao.ServiceProvider.GetRequiredService<IArmazenamentoExecucao>();
        var transacao = new TransacaoExecucao(armazenamento);
        var saldo = SaldoEstoque.De(ItemEstoque.Criar(_dados.Peca.Id, 10));
        await transacao.AdicionarAsync(ChavesExecucao.Estoque(_dados.FilialId, _dados.Peca.Id), saldo, CancellationToken.None);
        await transacao.ConfirmarAsync(CancellationToken.None);
    }

    public Task ConvergirAsync() => _mensageria.ConvergirAsync();

    public async Task<AndamentoOrdemServicoResponse> AndamentoAsync()
    {
        await using var scope = _os.CreateAsyncScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<ConsultarAndamentoOrdemServicoUseCase>().ExecuteAsync(new(Ordem.Id));
        resultado.Sucesso.Should().BeTrue();
        return resultado.Valor!;
    }

    public async Task DecidirAsync(DecisaoOrcamento decisao)
    {
        var andamento = await AndamentoAsync();
        andamento.OrcamentoId.Should().NotBeNull();
        await using var scope = _orcamento.CreateAsyncScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<RegistrarDecisaoOrcamentoUseCase>().ExecuteAsync(
            new(andamento.OrcamentoId!.Value, andamento.VersaoOrcamento, decisao, _operacaoDecisao, _dados.Cliente.Id));
        resultado.Sucesso.Should().BeTrue();
    }

    public async Task IniciarServicoAsync()
    {
        var andamento = await AndamentoAsync();
        await using var scope = _execucao.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IniciarExecucaoServicoUseCase>().ExecuteAsync(
            new(_operacaoInicio, Ordem.Id, andamento.PreparacaoId!.Value, Ordem.Servicos.Single().Id));
    }

    public async Task FinalizarExecucaoAsync()
    {
        var andamento = await AndamentoAsync();
        await using (var scope = _execucao.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<FinalizarServicoUseCase>().ExecuteAsync(
                new(_operacaoFimServico, Ordem.Id, andamento.PreparacaoId!.Value, Ordem.Servicos.Single().Id));
        await using (var scope = _execucao.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<FinalizarExecucaoUseCase>().ExecuteAsync(
                new(_operacaoFimExecucao, Ordem.Id, andamento.PreparacaoId!.Value));
    }

    public async Task ConfirmarPagamentoAsync()
    {
        await using var scope = _orcamento.CreateAsyncScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<ConferirPagamentoUseCase>().ExecuteAsync(new("123456", Guid.NewGuid()));
        resultado.Sucesso.Should().BeTrue();
    }

    public async Task<OrdemServicoResponse> EntregarAsync()
    {
        await using var scope = _os.CreateAsyncScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<EntregarOrdemServicoUseCase>().ExecuteAsync(new(Ordem.Id));
        resultado.Sucesso.Should().BeTrue();
        return resultado.Valor!;
    }

    public async Task<SaldoEstoque> SaldoAsync()
    {
        await using var scope = _execucao.CreateAsyncScope();
        var armazenamento = scope.ServiceProvider.GetRequiredService<IArmazenamentoExecucao>();
        return (await armazenamento.ObterAsync(ChavesExecucao.Estoque(_dados.FilialId, _dados.Peca.Id), CancellationToken.None)).Ler<SaldoEstoque>()!;
    }

    public async Task<SagaOrdemServico> SagaAsync()
    {
        await using var scope = _os.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OficinaMecanicaDbContext>().SagasOrdemServico
            .AsNoTracking().SingleAsync(saga => saga.OrdemServicoId == Ordem.Id);
    }

    public async Task<StatusOrcamento> StatusOrcamentoAsync()
    {
        await using var scope = _orcamento.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OrcamentoPagamentoDbContext>().Orcamentos
            .Where(orcamento => orcamento.OrdemServicoId == Ordem.Id).Select(orcamento => orcamento.Status).SingleAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _execucao.DisposeAsync();
        await _orcamento.DisposeAsync();
        await _os.DisposeAsync();
        _clienteDynamo.Dispose();
        var diretorio = Path.GetFullPath(_diretorio);
        var prefixoEsperado = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "oficina-bdd-");
        if (!diretorio.StartsWith(prefixoEsperado, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Diretório de teste fora da raiz temporária esperada.");
        if (Directory.Exists(diretorio)) Directory.Delete(diretorio, true);
    }
}
