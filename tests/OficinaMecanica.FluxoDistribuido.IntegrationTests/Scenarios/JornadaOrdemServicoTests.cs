using FluentAssertions;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.FluxoDistribuido.IntegrationTests.Fixtures;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Contratos;
using DecisaoOrcamento = OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums.DecisaoOrcamento;
using Xunit.Abstractions;

namespace OficinaMecanica.FluxoDistribuido.IntegrationTests.Scenarios;

public sealed class JornadaOrdemServicoTests(AmbienteDistribuidoFixture fixture, ITestOutputHelper saida) : IClassFixture<AmbienteDistribuidoFixture>
{
    [Fact]
    public async Task Dado_ReservaPersistidaESaidaBloqueada_Quando_InterromperEReiniciarProcesso_Entao_RetomaResultadoECompensaSemDuplicarSaldo()
    {
        await using var jornada = await fixture.CriarJornadaAsync();
        await using var processo = jornada.CriarProcessoExecucao();
        jornada.BloquearSaidaExecucao();
        await processo.IniciarAsync();
        var processoAnterior = processo.Id;
        await jornada.AbrirAsync();
        await AguardarProcessoAsync(jornada, processo, async () =>
            (await jornada.PendenciaExecucaoAsync()) is { Tentativas: > 0, LeaseAte: null });
        var pendente = (await jornada.PendenciaExecucaoAsync())!;
        pendente.Mensagem.Tipo.Should().Be("ExecucaoPreparada");
        pendente.UltimoErro.Should().Be("FALHA_PUBLICACAO");
        (await jornada.SaldoAsync()).Disponivel.Should().Be(7);
        (await jornada.SaldoAsync()).Reservada.Should().Be(3);
        (await jornada.SagaAsync()).Estado.Should().Be(EstadoSagaOrdemServico.Preparando);

        // Kill real: nenhum StopAsync/graceful shutdown confirma a Outbox antes de encerrar.
        await processo.InterromperAsync();
        saida.WriteLine("Processo {0} encerrado à força após tentativa persistida; resultado {1} pendente.", processoAnterior, pendente.Mensagem.MensagemId);
        var recuperavel = (await jornada.PendenciaExecucaoAsync())!;
        Execucao.Application.Mensagens.JsonContrato.Serializar(recuperavel.Mensagem)
            .Should().Be(Execucao.Application.Mensagens.JsonContrato.Serializar(pendente.Mensagem));
        await jornada.ReentregarPreparacaoAsync();
        jornada.LiberarSaidaExecucao();
        await processo.IniciarAsync();
        processo.Id.Should().NotBe(processoAnterior);
        saida.WriteLine("Novo processo {0} iniciado com a mesma tabela e arquivos locais.", processo.Id);
        await AguardarProcessoAsync(jornada, processo, async () =>
            (await jornada.SagaAsync()).Estado == EstadoSagaOrdemServico.AguardandoAprovacao
            && await jornada.PendenciaExecucaoAsync() is null);
        (await jornada.SaldoAsync()).Disponivel.Should().Be(7);
        (await jornada.SaldoAsync()).Reservada.Should().Be(3);
        await jornada.ConferirResultadoRecuperadoAsync(pendente.Mensagem.MensagemId);

        await jornada.SolicitarCancelamentoAsync();
        await jornada.SolicitarCancelamentoAsync();
        await AguardarProcessoAsync(jornada, processo, async () =>
            (await jornada.SagaAsync()).Estado == EstadoSagaOrdemServico.Compensada
            && await jornada.PendenciaExecucaoAsync() is null);
        var compensada = await jornada.SagaAsync();
        compensada.PreparacaoCancelada.Should().BeTrue();
        compensada.OrcamentoCancelado.Should().BeTrue();
        (await jornada.StatusOrcamentoAsync()).Should().Be(StatusOrcamento.Cancelado);
        (await jornada.SaldoAsync()).Disponivel.Should().Be(10);
        (await jornada.SaldoAsync()).Reservada.Should().Be(0);
        jornada.Provedor.QuantidadeCriacoes.Should().Be(0);
        await processo.InterromperAsync();
        await jornada.ConferirRecuperacaoAsync("OS", "Execucao", "Orcamento");
        saida.WriteLine("Mesmo resultado recebido pela OS; reserva 7/3 preservada e compensação final 10/0, sem cobrança.");
    }

    private static async Task AguardarProcessoAsync(JornadaDistribuida jornada, ProcessoExecucaoLocal processo, Func<Task<bool>> condicao)
    {
        // A lease real do publicador dura um minuto; não adiantar o relógio para recuperar um kill em voo.
        using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        while (!prazo.IsCancellationRequested)
        {
            processo.ConferirAtivo();
            await jornada.ConvergirAsync();
            if (await condicao()) return;
            await Task.Delay(100, prazo.Token);
        }
        throw new TimeoutException("A jornada não convergiu com o worker externo no prazo local.");
    }

    [Theory]
    [InlineData(StatusPagamentoProvedor.Pendente)]
    [InlineData(StatusPagamentoProvedor.Recusado)]
    [InlineData(StatusPagamentoProvedor.Desconhecido)]
    public async Task Dado_PagamentoNaoConfirmado_Quando_ConferirERepetir_Entao_BloqueiaEntregaAteConfirmacaoValida(StatusPagamentoProvedor status)
    {
        await using var jornada = await fixture.CriarJornadaAsync(falharPublicacao: true);
        await PrepararCobrancaAsync(jornada);
        jornada.Provedor.StatusConsulta = status;

        for (var tentativa = 0; tentativa < 2; tentativa++)
            (await jornada.ConferirPagamentoAsync()).Should().Be(status != StatusPagamentoProvedor.Desconhecido);
        await jornada.ConvergirAsync();

        await ConferirEntregaBloqueadaAsync(jornada);
        jornada.Provedor.StatusConsulta = StatusPagamentoProvedor.Confirmado;
        await ConfirmarEEntregarAsync(jornada);
    }

    [Theory]
    [InlineData("indisponivel")]
    [InlineData("moeda-divergente")]
    [InlineData("valor-divergente")]
    public async Task Dado_ConsultaFinanceiraComFalha_Quando_ConferirERepetir_Entao_NaoConfirmaEPermiteRetomadaValida(string falha)
    {
        await using var jornada = await fixture.CriarJornadaAsync(falharPublicacao: true);
        await PrepararCobrancaAsync(jornada);
        jornada.Provedor.ConsultaIndisponivel = falha == "indisponivel";
        jornada.Provedor.MoedaConsulta = falha == "moeda-divergente" ? "USD" : "BRL";
        jornada.Provedor.DiferencaValor = falha == "valor-divergente" ? 1m : 0m;

        for (var tentativa = 0; tentativa < 2; tentativa++)
        {
            if (falha == "indisponivel")
                await Assert.ThrowsAsync<HttpRequestException>(() => jornada.ConferirPagamentoAsync());
            else
                await Assert.ThrowsAsync<OrcamentoPagamento.Application.Mensagens.ContratoMensagemException>(() => jornada.ConferirPagamentoAsync());
        }
        await jornada.ConvergirAsync();

        await ConferirEntregaBloqueadaAsync(jornada);
        jornada.Provedor.ConsultaIndisponivel = false;
        jornada.Provedor.MoedaConsulta = "BRL";
        jornada.Provedor.DiferencaValor = 0m;
        await ConfirmarEEntregarAsync(jornada);
    }

    private static async Task PrepararCobrancaAsync(JornadaDistribuida jornada)
    {
        await jornada.AbrirAsync();
        await jornada.ConvergirAsync();
        await jornada.DecidirAsync(DecisaoOrcamento.Aprovado);
        await jornada.ConvergirAsync();
        await jornada.IniciarServicoAsync();
        await jornada.ConvergirAsync();
        await jornada.FinalizarExecucaoAsync();
        await jornada.ConvergirAsync();
        jornada.Provedor.QuantidadeCriacoes.Should().Be(1);
    }

    private static async Task ConferirEntregaBloqueadaAsync(JornadaDistribuida jornada)
    {
        (await jornada.SagaAsync()).Estado.Should().Be(EstadoSagaOrdemServico.AguardandoPagamento);
        (await jornada.AndamentoAsync()).PagamentoConfirmado.Should().BeFalse();
        (await jornada.SaldoAsync()).Disponivel.Should().Be(7);
        (await jornada.SaldoAsync()).Reservada.Should().Be(0);
        jornada.Provedor.QuantidadeCriacoes.Should().Be(1);
        await Assert.ThrowsAsync<Domain.Shared.Exceptions.DomainException>(() => jornada.EntregarAsync());
    }

    private static async Task ConfirmarEEntregarAsync(JornadaDistribuida jornada)
    {
        await jornada.ConfirmarPagamentoAsync();
        await jornada.ConfirmarPagamentoAsync();
        await jornada.ConvergirAsync();
        (await jornada.EntregarAsync()).Status.Should().Be(nameof(StatusOrdemServico.Entregue));
        (await jornada.SagaAsync()).Estado.Should().Be(EstadoSagaOrdemServico.PagamentoConfirmado);
        jornada.Provedor.QuantidadeCriacoes.Should().Be(1);
        (await jornada.SaldoAsync()).Disponivel.Should().Be(7);
        (await jornada.SaldoAsync()).Reservada.Should().Be(0);
        await jornada.ConferirRecuperacaoAsync("OS", "Execucao", "Orcamento");
    }

    [Theory]
    [InlineData("ausente")]
    [InlineData("valido")]
    [InlineData("campos-proibidos")]
    [InlineData("invalido")]
    public async Task Dado_ContextoTecnico_Quando_RetomarEReentregarComOutroTrace_Entao_PreservaContratoEEfeitosUnicos(string cenario)
    {
        await using var jornada = await fixture.CriarJornadaAsync(falharPublicacao: true);
        var contexto = new ContextoTraceJornada(cenario);
        await jornada.AbrirAsync();
        await jornada.DefinirContextoPreparacaoAsync(contexto);
        await jornada.ConvergirAsync();
        await jornada.DecidirAsync(DecisaoOrcamento.Aprovado);
        await jornada.ConvergirAsync();
        (await jornada.SagaAsync()).InicioFisicoConfirmado.Should().BeFalse();

        await jornada.IniciarServicoAsync();
        await jornada.ConvergirAsync();
        await jornada.FinalizarExecucaoAsync();
        await jornada.ConvergirAsync();
        await jornada.ConfirmarPagamentoAsync();
        await jornada.ConvergirAsync();
        var entregue = await jornada.EntregarAsync();

        entregue.Status.Should().Be(nameof(StatusOrdemServico.Entregue));
        (await jornada.SaldoAsync()).Disponivel.Should().Be(7);
        (await jornada.SaldoAsync()).Reservada.Should().Be(0);
        jornada.Provedor.QuantidadeCriacoes.Should().Be(1);
        await jornada.ConferirRecuperacaoAsync("OS", "Execucao", "Orcamento");
        contexto.ConferirJornada();
        jornada.Logs.ConferirCarriersProibidos();
    }

    [Fact]
    public async Task Dado_OrdemPreparada_Quando_RepetirCancelamento_Entao_CompensaAmbosSemDuplicarLiberacao()
    {
        await using var jornada = await fixture.CriarJornadaAsync();
        await jornada.AbrirAsync();
        await jornada.ConvergirAsync();
        (await jornada.SaldoAsync()).Reservada.Should().Be(3);

        await jornada.SolicitarCancelamentoAsync();
        await jornada.SolicitarCancelamentoAsync();
        var pendente = await jornada.SagaAsync();
        pendente.Estado.Should().Be(EstadoSagaOrdemServico.CompensacaoPendente);
        pendente.PreparacaoCancelada.Should().BeFalse();
        pendente.OrcamentoCancelado.Should().BeFalse();
        await jornada.ConvergirAsync();
        await jornada.ConvergirAsync();

        var compensada = await jornada.SagaAsync();
        compensada.Estado.Should().Be(EstadoSagaOrdemServico.Compensada);
        compensada.PreparacaoCancelada.Should().BeTrue();
        compensada.OrcamentoCancelado.Should().BeTrue();
        (await jornada.StatusOrcamentoAsync()).Should().Be(StatusOrcamento.Cancelado);
        (await jornada.SaldoAsync()).Disponivel.Should().Be(10);
        (await jornada.SaldoAsync()).Reservada.Should().Be(0);
        jornada.Provedor.QuantidadeCriacoes.Should().Be(0);
    }

    [Fact]
    public async Task Dado_InicioFisicoConfirmado_Quando_Cancelar_Entao_RecusaEPreservaReservaEOrcamento()
    {
        await using var jornada = await fixture.CriarJornadaAsync();
        await jornada.AbrirAsync();
        await jornada.ConvergirAsync();
        await jornada.DecidirAsync(DecisaoOrcamento.Aprovado);
        await jornada.ConvergirAsync();
        await jornada.IniciarServicoAsync();
        await jornada.ConvergirAsync();

        await Assert.ThrowsAsync<Domain.Shared.Exceptions.DomainException>(() => jornada.SolicitarCancelamentoAsync());
        await Assert.ThrowsAsync<Domain.Shared.Exceptions.DomainException>(() => jornada.SolicitarCancelamentoAsync());
        await jornada.ConvergirAsync();

        var saga = await jornada.SagaAsync();
        saga.Estado.Should().Be(EstadoSagaOrdemServico.EmExecucao);
        saga.InicioFisicoConfirmado.Should().BeTrue();
        saga.PreparacaoCancelada.Should().BeFalse();
        saga.OrcamentoCancelado.Should().BeFalse();
        (await jornada.StatusOrcamentoAsync()).Should().Be(StatusOrcamento.Aprovado);
        (await jornada.SaldoAsync()).Disponivel.Should().Be(7);
        (await jornada.SaldoAsync()).Reservada.Should().Be(3);
        jornada.Provedor.QuantidadeCriacoes.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_OrdemComSaldo_Quando_AprovarExecutarEPagar_Entao_EntregaSemDuplicarReservaOuCobranca(bool falharPublicacao)
    {
        // Arrange — três serviços reais, com dois bancos SQL e DynamoDB Local.
        await using var jornada = await fixture.CriarJornadaAsync(falharPublicacao);
        await jornada.AbrirAsync();

        // Act — cada transporte reentrega o JSON e cada operação reabre seu escopo.
        await jornada.ConvergirAsync();
        (await jornada.SaldoAsync()).Reservada.Should().Be(3);
        await jornada.DecidirAsync(DecisaoOrcamento.Aprovado);
        await jornada.DecidirAsync(DecisaoOrcamento.Aprovado);
        await jornada.ConvergirAsync();
        (await jornada.SagaAsync()).InicioFisicoConfirmado.Should().BeFalse();
        await jornada.IniciarServicoAsync();
        await jornada.IniciarServicoAsync();
        await jornada.ConvergirAsync();
        await jornada.FinalizarExecucaoAsync();
        await jornada.FinalizarExecucaoAsync();
        await jornada.ConvergirAsync();
        (await jornada.AndamentoAsync()).PagamentoConfirmado.Should().BeFalse();
        await Assert.ThrowsAsync<Domain.Shared.Exceptions.DomainException>(() => jornada.EntregarAsync());
        await jornada.ConfirmarPagamentoAsync();
        await jornada.ConfirmarPagamentoAsync();
        await jornada.ConvergirAsync();
        var entregue = await jornada.EntregarAsync();

        // Assert — efeitos únicos e entrega após confirmação financeira.
        entregue.Status.Should().Be(nameof(StatusOrdemServico.Entregue));
        (await jornada.SagaAsync()).Estado.Should().Be(EstadoSagaOrdemServico.PagamentoConfirmado);
        (await jornada.SaldoAsync()).Disponivel.Should().Be(7);
        (await jornada.SaldoAsync()).Reservada.Should().Be(0);
        jornada.Provedor.QuantidadeCriacoes.Should().Be(1);
        await jornada.ConferirRecuperacaoAsync("OS", "Execucao", "Orcamento");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_OrdemPreparada_Quando_RecusarOrcamento_Entao_CompensaParticipantesERestauraSaldoUmaVez(bool falharPublicacao)
    {
        // Arrange
        await using var jornada = await fixture.CriarJornadaAsync(falharPublicacao);
        await jornada.AbrirAsync();
        await jornada.ConvergirAsync();
        (await jornada.SaldoAsync()).Reservada.Should().Be(3);

        // Act
        await jornada.DecidirAsync(DecisaoOrcamento.Recusado);
        await jornada.ConvergirAsync();
        await jornada.ConvergirAsync();

        // Assert
        var saga = await jornada.SagaAsync();
        saga.Estado.Should().Be(EstadoSagaOrdemServico.Compensada);
        saga.PreparacaoCancelada.Should().BeTrue();
        saga.OrcamentoCancelado.Should().BeTrue();
        (await jornada.StatusOrcamentoAsync()).Should().Be(StatusOrcamento.Cancelado);
        (await jornada.SaldoAsync()).Disponivel.Should().Be(10);
        (await jornada.SaldoAsync()).Reservada.Should().Be(0);
        jornada.Provedor.QuantidadeCriacoes.Should().Be(0);
        await jornada.ConferirRecuperacaoAsync("OS", "Execucao", "Orcamento");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_SaldoInsuficiente_Quando_AbrirOrdem_Entao_RecusaSemReservaParcialOuCobranca(bool falharPublicacao)
    {
        // Arrange
        await using var jornada = await fixture.CriarJornadaAsync(falharPublicacao);
        await jornada.AbrirAsync(quantidade: 11);

        // Act
        await jornada.ConvergirAsync();

        // Assert
        (await jornada.SagaAsync()).Estado.Should().Be(EstadoSagaOrdemServico.PreparacaoRecusada);
        (await jornada.SaldoAsync()).Disponivel.Should().Be(10);
        (await jornada.SaldoAsync()).Reservada.Should().Be(0);
        (await jornada.AndamentoAsync()).OrcamentoId.Should().BeNull();
        jornada.Provedor.QuantidadeCriacoes.Should().Be(0);
        await jornada.ConferirRecuperacaoAsync("OS", "Execucao");
    }
}
