using FluentAssertions;
using Moq;
using OficinaMecanica.Application.Common;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ConsultarStatusOrdemServico;
using OficinaMecanica.Application.UnitTests.Common;
using OficinaMecanica.Application.UnitTests.GestaoOrdemServico.Factories;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Domain.GestaoOrdemServico.Messages;

namespace OficinaMecanica.Application.UnitTests.GestaoOrdemServico.OrdemServicoUseCases.ConsultarStatusOrdemServico;

public class ConsultarStatusOrdemServicoUseCaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_OrdemDistribuidaComSnapshot_Quando_ConsultarStatus_Entao_NaoDeveApresentarEstadoFisicoLegado(bool preparada)
    {
        // Arrange
        var ordem = OrdemServicoTestDataFactory.CriarOrdemServicoEmExecucaoComServicoPendente();
        var saga = SagaOrdemServico.Iniciar(ordem.Id, 1, 1);
        var preparacaoId = Guid.NewGuid();
        if (preparada)
        {
            saga.RegistrarPreparacao(preparacaoId, 1);
        }
        var sagas = new Mock<ISagaOrdemServicoRepository>();
        sagas.Setup(repo => repo.ObterPorOrdemAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(saga);
        var useCase = new ConsultarStatusOrdemServicoUseCase(CriarRepository(ordem).Object, sagas.Object,
            new ConsultarStatusOrdemServicoValidator(), MapperFactory.Criar());

        // Act
        var resultado = await useCase.ExecuteAsync(new ConsultarStatusOrdemServicoRequest(ordem.Id));

        // Assert
        resultado.Sucesso.Should().BeTrue();
        resultado.Valor!.Status.Should().Be(OrdemServicoTestDataFactory.StatusEmExecucao);
        resultado.Valor.ExecucaoDistribuida.Should().BeTrue();
        resultado.Valor.PreparacaoId.Should().Be(preparada ? preparacaoId : null);
        resultado.Valor.Servicos.Should().BeEmpty();
        ordem.Servicos.Should().ContainSingle().Which.Status.ToString().Should().Be(OrdemServicoTestDataFactory.StatusPendente);
        sagas.Verify(repo => repo.ObterPorOrdemAsync(ordem.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dado_OrdemServicoExistente_Quando_ConsultarStatusOrdemServico_Entao_DeveRetornarStatusDaOrdemServico()
    {
        // Arrange
        var ordemServico = OrdemServicoTestDataFactory.CriarOrdemServicoEmExecucaoComServicoEmExecucao();

        var servico = ordemServico.Servicos.Single();

        var repository = CriarRepository(ordemServico);

        var useCase = CriarUseCase(repository);

        var request = OrdemServicoTestDataFactory.CriarConsultarStatusOrdemServicoRequestValido(ordemServico.Id);

        // Act
        var resultado = await useCase.ExecuteAsync(request);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        resultado.Valor.Should().NotBeNull();
        resultado.Valor!.OrdemServicoId.Should().Be(ordemServico.Id);
        resultado.Valor.Numero.Should().Be(ordemServico.Numero);
        resultado.Valor.Status.Should().Be(OrdemServicoTestDataFactory.StatusEmExecucao);
        resultado.Valor.Servicos.Should().ContainSingle();
        resultado.Valor.Servicos.Single().ServicoId.Should().Be(servico.Id);
        resultado.Valor.Servicos.Single().ServicoCatalogoId.Should().Be(servico.ServicoCatalogoId);
        resultado.Valor.Servicos.Single().Status.Should().Be(OrdemServicoTestDataFactory.StatusEmExecucao);

        repository.Verify(repo => repo.ObterPorIdAsync(request.OrdemServicoId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dado_OrdemServicoSemServicos_Quando_ConsultarStatusOrdemServico_Entao_DeveRetornarStatusSemServicos()
    {
        // Arrange
        var ordemServico = OrdemServicoTestDataFactory.CriarOrdemServicoRecebida();

        var repository = CriarRepository(ordemServico);

        var useCase = CriarUseCase(repository);

        var request = OrdemServicoTestDataFactory.CriarConsultarStatusOrdemServicoRequestValido(ordemServico.Id);

        // Act
        var resultado = await useCase.ExecuteAsync(request);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        resultado.Valor.Should().NotBeNull();
        resultado.Valor!.OrdemServicoId.Should().Be(ordemServico.Id);
        resultado.Valor.Numero.Should().Be(ordemServico.Numero);
        resultado.Valor.Status.Should().Be(OrdemServicoTestDataFactory.StatusRecebida);
        resultado.Valor.Servicos.Should().BeEmpty();

        repository.Verify(repo => repo.ObterPorIdAsync(request.OrdemServicoId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dado_OrdemServicoInexistente_Quando_ConsultarStatusOrdemServico_Entao_DeveRetornarFalha()
    {
        // Arrange
        var repository = CriarRepository(null);

        var useCase = CriarUseCase(repository);

        var request = OrdemServicoTestDataFactory.CriarConsultarStatusOrdemServicoRequestValido();

        // Act
        var resultado = await useCase.ExecuteAsync(request);

        // Assert
        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().NotBeNull();
        resultado.Erro!.Mensagem.Should().Be(OrdemServicoErrorMessages.OrdemServicoNaoEncontrada);
        resultado.Erro.Tipo.Should().Be(TipoErro.NaoEncontrado);

        repository.Verify(repo => repo.ObterPorIdAsync(request.OrdemServicoId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dado_IdVazio_Quando_ConsultarStatusOrdemServico_Entao_DeveRetornarFalhaDeValidacao()
    {
        // Arrange
        var repository = new Mock<IOrdemServicoRepository>();

        var useCase = CriarUseCase(repository);

        var request = OrdemServicoTestDataFactory.CriarConsultarStatusOrdemServicoRequestValido(Guid.Empty);

        // Act
        var resultado = await useCase.ExecuteAsync(request);

        // Assert
        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().NotBeNull();
        resultado.Erro!.Mensagem.Should().NotBeNullOrWhiteSpace();
        resultado.Erro.Tipo.Should().Be(TipoErro.Validacao);

        repository.Verify(repo => repo.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IOrdemServicoRepository> CriarRepository(OrdemServico? ordemServico)
    {
        var repository = new Mock<IOrdemServicoRepository>();

        repository
            .Setup(repo => repo.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordemServico);

        return repository;
    }

    private static ConsultarStatusOrdemServicoUseCase CriarUseCase(Mock<IOrdemServicoRepository> repository)
    {
        return new ConsultarStatusOrdemServicoUseCase(repository.Object, Mock.Of<ISagaOrdemServicoRepository>(),
            new ConsultarStatusOrdemServicoValidator(), MapperFactory.Criar());
    }
}
