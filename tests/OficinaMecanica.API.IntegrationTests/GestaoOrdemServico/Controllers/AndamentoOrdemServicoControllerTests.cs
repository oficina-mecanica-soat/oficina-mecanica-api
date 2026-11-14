using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OficinaMecanica.API.IntegrationTests.Fixtures;
using OficinaMecanica.API.IntegrationTests.GestaoOrdemServico.Factories;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.Responses;
using OficinaMecanica.Domain.GestaoOrdemServico.Enums;

namespace OficinaMecanica.API.IntegrationTests.GestaoOrdemServico.Controllers;

[Collection(OficinaMecanicaApiCollection.Nome)]
public sealed class AndamentoOrdemServicoControllerTests : ApiIntegrationTestBase
{
    private const string Rota = "/api/v1/gestao-ordem-servico/ordens-servico";

    public AndamentoOrdemServicoControllerTests(OficinaMecanicaApiFixture fixture) : base(fixture)
    {
    }

    private async Task<(Guid OrdemServicoId, Guid ClienteId, Guid? PagamentoId)> PrepararAsync(bool cobrar = true, bool confirmar = false, bool criarSaga = true)
    {
        await using var dbContext = Fixture.CriarDbContext();
        var request = await SagaIntegracaoTestDataFactory.CriarAsync(dbContext, criarSaga);
        Guid? pagamentoId = null;
        if (criarSaga && cobrar)
        {
            var saga = await dbContext.SagasOrdemServico.SingleAsync();
            saga.RegistrarOrcamento(Guid.NewGuid(), 3, 150m);
            saga.RegistrarDecisao(saga.OrcamentoId!.Value, 3, DecisaoOrcamento.Aprovado);
            saga.RegistrarInicioFisico(saga.PreparacaoId!.Value, 3);
            saga.RegistrarFinalizacao(saga.PreparacaoId.Value, 3, DateTimeOffset.UtcNow);
            pagamentoId = Guid.NewGuid();
            saga.RegistrarCobranca(saga.OrcamentoId.Value, 3, pagamentoId.Value, 150m);
            if (confirmar)
            {
                saga.RegistrarPagamento(saga.OrcamentoId.Value, 3, pagamentoId.Value, 150m);
            }
            await dbContext.SaveChangesAsync();
        }
        return (request.OrdemServicoId, request.Dados.ClienteId, pagamentoId);
    }

    private void Autenticar(string? papel, string? clienteId = null)
    {
        Client.DefaultRequestHeaders.Authorization = null;
        if (papel is null)
        {
            return;
        }
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new("role", papel),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (clienteId is not null)
        {
            claims.Add(new Claim("cliente_id", clienteId));
        }
        var credenciais = new SigningCredentials(new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("oficina-mecanica-api-chave-academica-jwt-2026")), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("oficina-mecanica-auth", "oficina-mecanica-api",
            claims, expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: credenciais));
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Theory]
    [InlineData(null, false, HttpStatusCode.Unauthorized)]
    [InlineData("Visitante", false, HttpStatusCode.Forbidden)]
    [InlineData("Cliente", false, HttpStatusCode.Forbidden)]
    [InlineData("Cliente", true, HttpStatusCode.OK)]
    [InlineData("Administrador", false, HttpStatusCode.OK)]
    [InlineData("Atendente", false, HttpStatusCode.OK)]
    [InlineData("Mecanico", false, HttpStatusCode.OK)]
    public async Task Dado_PerfilDoUsuario_Quando_ConsultarAndamento_Entao_DeveRespeitarAutorizacao(string? papel, bool proprietario, HttpStatusCode esperado)
    {
        // Arrange
        var dados = await PrepararAsync();
        Autenticar(papel, proprietario ? dados.ClienteId.ToString() : null);

        // Act
        using var response = await Client.GetAsync($"{Rota}/{dados.OrdemServicoId}/consultar-andamento");

        // Assert
        response.StatusCode.Should().Be(esperado);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_CobrancaConhecida_Quando_Consultar_Entao_DeveSepararIdentidadeDeConfirmacaoFinanceira(bool confirmado)
    {
        // Arrange
        var dados = await PrepararAsync(confirmar: confirmado);
        Autenticar("Cliente", dados.ClienteId.ToString());

        // Act
        using var response = await Client.GetAsync($"{Rota}/{dados.OrdemServicoId}/consultar-andamento");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var andamento = await response.Content.ReadFromJsonAsync<AndamentoOrdemServicoResponse>();
        andamento!.PagamentoId.Should().Be(dados.PagamentoId);
        andamento.PagamentoConfirmado.Should().Be(confirmado);
        andamento.Estado.Should().Be(confirmado ? "PagamentoConfirmado" : "AguardandoPagamento");
        andamento.ValorOrcamento.Should().Be(150m);
        andamento.VersaoOrcamento.Should().Be(3);
    }

    [Fact]
    public async Task Dado_OrcamentoAindaNaoGerado_Quando_Consultar_Entao_NaoDeveInventarCobrancaOuPagamento()
    {
        // Arrange
        var dados = await PrepararAsync(cobrar: false);
        Autenticar("Administrador");

        // Act
        using var response = await Client.GetAsync($"{Rota}/{dados.OrdemServicoId}/consultar-andamento");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var andamento = await response.Content.ReadFromJsonAsync<AndamentoOrdemServicoResponse>();
        andamento!.Estado.Should().Be("AguardandoOrcamento");
        andamento.OrcamentoId.Should().BeNull();
        andamento.PagamentoId.Should().BeNull();
        andamento.PagamentoConfirmado.Should().BeFalse();
    }

    [Theory]
    [InlineData("outro-cliente", HttpStatusCode.NotFound)]
    [InlineData("invalido", HttpStatusCode.Forbidden)]
    [InlineData("vazio", HttpStatusCode.Forbidden)]
    public async Task Dado_ClienteIncompativel_Quando_Consultar_Entao_NaoDeveExporAndamento(string tipo, HttpStatusCode esperado)
    {
        // Arrange
        var dados = await PrepararAsync();
        var clienteId = tipo == "outro-cliente" ? Guid.NewGuid().ToString() : tipo == "vazio" ? Guid.Empty.ToString() : "invalido";
        Autenticar("Cliente", clienteId);

        // Act
        using var response = await Client.GetAsync($"{Rota}/{dados.OrdemServicoId}/consultar-andamento");

        // Assert
        response.StatusCode.Should().Be(esperado);
    }

    [Theory]
    [InlineData("inexistente", HttpStatusCode.NotFound)]
    [InlineData("sem-saga", HttpStatusCode.NotFound)]
    [InlineData("vazio", HttpStatusCode.BadRequest)]
    public async Task Dado_OrdemSemAndamentoValido_Quando_Consultar_Entao_DeveClassificarFalha(string tipo, HttpStatusCode esperado)
    {
        // Arrange
        var dados = await PrepararAsync(criarSaga: tipo != "sem-saga");
        var ordemId = tipo == "inexistente" ? Guid.NewGuid() : tipo == "vazio" ? Guid.Empty : dados.OrdemServicoId;
        Autenticar("Administrador");

        // Act
        using var response = await Client.GetAsync($"{Rota}/{ordemId}/consultar-andamento");

        // Assert
        response.StatusCode.Should().Be(esperado);
    }
}
