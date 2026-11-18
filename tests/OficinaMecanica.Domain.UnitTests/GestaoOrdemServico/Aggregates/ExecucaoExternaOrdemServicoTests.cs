using FluentAssertions;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Domain.Shared.Exceptions;

namespace OficinaMecanica.Domain.UnitTests.GestaoOrdemServico.Aggregates;

public sealed class ExecucaoExternaOrdemServicoTests
{
    [Fact]
    public void Dado_ExecucaoExterna_Quando_RegistrarFinalizacao_Entao_DeveAtualizarSomenteEstadoDaOrdemSemSimularServicos()
    {
        // Arrange
        var ordem = OrdemServico.Abrir(1, Guid.NewGuid(), Guid.NewGuid());
        ordem.RegistrarServicoNaAbertura(Guid.NewGuid(), 100m);
        var finalizadoEm = DateTimeOffset.UtcNow;

        // Act
        ordem.RegistrarInicioExecucaoExterna();
        ordem.RegistrarFinalizacaoExecucaoExterna(finalizadoEm);
        ordem.RegistrarFinalizacaoExecucaoExterna(finalizadoEm);

        // Assert
        ordem.Status.Should().Be(StatusOrdemServico.Finalizada);
        ordem.DataFim.Should().Be(finalizadoEm.UtcDateTime);
        ordem.Servicos.Single().Status.Should().Be(StatusServico.Pendente);
    }

    [Fact]
    public void Dado_OrdemFinalizada_Quando_ReceberInicioExterno_Entao_NaoDeveRegredirEstado()
    {
        // Arrange
        var ordem = OrdemServico.Abrir(1, Guid.NewGuid(), Guid.NewGuid());
        ordem.RegistrarInicioExecucaoExterna();
        ordem.RegistrarFinalizacaoExecucaoExterna(DateTimeOffset.UtcNow);

        // Act
        var acao = () => ordem.RegistrarInicioExecucaoExterna();

        // Assert
        acao.Should().Throw<DomainException>();
        ordem.Status.Should().Be(StatusOrdemServico.Finalizada);
    }

    [Fact]
    public void Dado_OrdemSemInicio_Quando_RegistrarFinalizacaoExterna_Entao_DeveRecusar()
    {
        // Arrange
        var ordem = OrdemServico.Abrir(1, Guid.NewGuid(), Guid.NewGuid());

        // Act
        var acao = () => ordem.RegistrarFinalizacaoExecucaoExterna(DateTimeOffset.UtcNow);

        // Assert
        acao.Should().Throw<DomainException>();
        ordem.Status.Should().Be(StatusOrdemServico.Recebida);
    }

    [Fact]
    public void Dado_DataInvalida_Quando_RegistrarFinalizacaoExterna_Entao_DeveRecusarSemAlterarOrdem()
    {
        // Arrange
        var ordem = OrdemServico.Abrir(1, Guid.NewGuid(), Guid.NewGuid());
        ordem.RegistrarInicioExecucaoExterna();

        // Act
        var acao = () => ordem.RegistrarFinalizacaoExecucaoExterna(default);

        // Assert
        acao.Should().Throw<DomainException>();
        ordem.Status.Should().Be(StatusOrdemServico.EmExecucao);
    }
}
