using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OficinaMecanica.Application.Common;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.EntregarOrdemServico;
using OficinaMecanica.Application.UnitTests.Common;
using OficinaMecanica.Application.UnitTests.GestaoOrdemServico.Factories;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Domain.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Domain.GestaoOrdemServico.Messages;
using OficinaMecanica.Domain.Shared.Exceptions;

namespace OficinaMecanica.Application.UnitTests.GestaoOrdemServico.OrdemServicoUseCases.EntregarOrdemServico;

public class EntregarOrdemServicoUseCaseTests
{
    [Fact]
    public async Task Dado_OrdemServicoFinalizada_Quando_EntregarOrdemServico_Entao_DeveAtualizarStatusEPersistir()
    {
        // Arrange
        var ordemServico = OrdemServicoTestDataFactory.CriarOrdemServicoFinalizada();

        var repository = CriarRepository(ordemServico);

        var useCase = CriarUseCase(repository);

        var request = OrdemServicoTestDataFactory.CriarEntregarOrdemServicoRequestValido(ordemServico.Id);

        // Act
        var resultado = await useCase.ExecuteAsync(request);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        resultado.Valor.Should().NotBeNull();
        resultado.Valor!.Id.Should().Be(ordemServico.Id);
        resultado.Valor.Status.Should().Be(OrdemServicoTestDataFactory.StatusEntregue);

        repository.Verify(repo => repo.ObterPorIdAsync(request.OrdemServicoId, It.IsAny<CancellationToken>()), Times.Once);

        repository.Verify(
            repo => repo.AtualizarAsync(
                It.Is<OrdemServico>(ordemServicoAtualizada =>
                    ordemServicoAtualizada.Id == ordemServico.Id
                    && ordemServicoAtualizada.Status.ToString() == OrdemServicoTestDataFactory.StatusEntregue),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Dado_OrdemServicoInexistente_Quando_EntregarOrdemServico_Entao_DeveRetornarFalha()
    {
        // Arrange
        var repository = CriarRepository(null);

        var useCase = CriarUseCase(repository);

        var request = OrdemServicoTestDataFactory.CriarEntregarOrdemServicoRequestValido();

        // Act
        var resultado = await useCase.ExecuteAsync(request);

        // Assert
        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().NotBeNull();
        resultado.Erro!.Mensagem.Should().Be(OrdemServicoErrorMessages.OrdemServicoNaoEncontrada);
        resultado.Erro.Tipo.Should().Be(TipoErro.NaoEncontrado);

        repository.Verify(repo => repo.ObterPorIdAsync(request.OrdemServicoId, It.IsAny<CancellationToken>()), Times.Once);

        repository.Verify(repo => repo.AtualizarAsync(It.IsAny<OrdemServico>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Dado_OrdemServicoEmExecucao_Quando_EntregarOrdemServico_Entao_DeveLancarDomainException()
    {
        // Arrange
        var ordemServico = OrdemServicoTestDataFactory.CriarOrdemServicoEmExecucaoComServicoFinalizado();

        var repository = CriarRepository(ordemServico);

        var useCase = CriarUseCase(repository);

        var request = OrdemServicoTestDataFactory.CriarEntregarOrdemServicoRequestValido(ordemServico.Id);

        // Act
        var acao = () => useCase.ExecuteAsync(request);

        // Assert
        await acao.Should()
            .ThrowAsync<DomainException>()
            .WithMessage(OrdemServicoErrorMessages.TransicaoStatusInvalida);

        repository.Verify(repo => repo.ObterPorIdAsync(request.OrdemServicoId, It.IsAny<CancellationToken>()), Times.Once);

        repository.Verify(repo => repo.AtualizarAsync(It.IsAny<OrdemServico>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Dado_IdVazio_Quando_EntregarOrdemServico_Entao_DeveRetornarFalhaDeValidacao()
    {
        // Arrange
        var repository = new Mock<IOrdemServicoRepository>();

        var useCase = CriarUseCase(repository);

        var request = OrdemServicoTestDataFactory.CriarEntregarOrdemServicoRequestValido(Guid.Empty);

        // Act
        var resultado = await useCase.ExecuteAsync(request);

        // Assert
        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().NotBeNull();
        resultado.Erro!.Mensagem.Should().NotBeNullOrWhiteSpace();
        resultado.Erro.Tipo.Should().Be(TipoErro.Validacao);

        repository.Verify(repo => repo.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        repository.Verify(repo => repo.AtualizarAsync(It.IsAny<OrdemServico>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_SagaComCobranca_Quando_Entregar_Entao_ExigirConfirmacaoDePagamento(bool confirmado)
    {
        // Arrange
        var ordem = OrdemServicoTestDataFactory.CriarOrdemServicoFinalizada();
        var repository = CriarRepository(ordem);
        var saga = CriarSagaComCobranca(ordem.Id);
        if (confirmado)
        {
            saga.RegistrarPagamento(saga.OrcamentoId!.Value, 1, saga.PagamentoId!.Value, 150m);
        }
        var sagaRepository = new Mock<ISagaOrdemServicoRepository>();
        sagaRepository.Setup(repo => repo.ObterPorOrdemAsync(ordem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(saga);
        var useCase = CriarUseCase(repository, sagaRepository);

        // Act
        var acao = () => useCase.ExecuteAsync(new(ordem.Id));

        // Assert
        if (confirmado)
        {
            var resultado = await acao();
            resultado.Sucesso.Should().BeTrue();
            ordem.Status.Should().Be(StatusOrdemServico.Entregue);
            repository.Verify(repo => repo.AtualizarAsync(ordem, It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            await acao.Should().ThrowAsync<DomainException>().WithMessage(SagaOrdemServicoErrorMessages.PagamentoNaoConfirmadoParaEntrega);
            ordem.Status.Should().Be(StatusOrdemServico.Finalizada);
            repository.Verify(repo => repo.AtualizarAsync(It.IsAny<OrdemServico>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task Dado_FalhaAoConsultarSaga_Quando_Entregar_Entao_NaoPresumirOrdemLegadaNemPersistirEntrega()
    {
        // Arrange
        var ordem = OrdemServicoTestDataFactory.CriarOrdemServicoFinalizada();
        var repository = CriarRepository(ordem);
        var sagaRepository = new Mock<ISagaOrdemServicoRepository>();
        sagaRepository.Setup(repo => repo.ObterPorOrdemAsync(ordem.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Persistência indisponível."));
        var useCase = CriarUseCase(repository, sagaRepository);

        // Act
        var acao = () => useCase.ExecuteAsync(new(ordem.Id));

        // Assert
        await acao.Should().ThrowAsync<IOException>();
        ordem.Status.Should().Be(StatusOrdemServico.Finalizada);
        repository.Verify(repo => repo.AtualizarAsync(It.IsAny<OrdemServico>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SagaOrdemServico CriarSagaComCobranca(Guid ordemServicoId)
    {
        var saga = SagaOrdemServico.Iniciar(ordemServicoId, 1, 1);
        saga.RegistrarPreparacao(Guid.NewGuid(), 1);
        saga.RegistrarOrcamento(Guid.NewGuid(), 1, 150m);
        saga.RegistrarDecisao(saga.OrcamentoId!.Value, 1, DecisaoOrcamento.Aprovado);
        saga.RegistrarInicioFisico(saga.PreparacaoId!.Value, 1);
        saga.RegistrarFinalizacao(saga.PreparacaoId.Value, 1, DateTimeOffset.UtcNow);
        saga.RegistrarCobranca(saga.OrcamentoId.Value, 1, Guid.NewGuid(), 150m);
        return saga;
    }

    private static Mock<IOrdemServicoRepository> CriarRepository(OrdemServico? ordemServico)
    {
        var repository = new Mock<IOrdemServicoRepository>();

        repository
            .Setup(repo => repo.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ordemServico);

        return repository;
    }

    private static EntregarOrdemServicoUseCase CriarUseCase(Mock<IOrdemServicoRepository> repository, Mock<ISagaOrdemServicoRepository>? sagaRepository = null)
    {
        return new EntregarOrdemServicoUseCase(repository.Object, (sagaRepository ?? new Mock<ISagaOrdemServicoRepository>()).Object, new EntregarOrdemServicoValidator(), MapperFactory.Criar(), NullLogger<EntregarOrdemServicoUseCase>.Instance);
    }
}
