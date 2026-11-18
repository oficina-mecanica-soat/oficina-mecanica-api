using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using OficinaMecanica.API.IntegrationTests.Fixtures;
using OficinaMecanica.Application.Atendimento.ClienteUseCases.ConsultarClienteAutenticacao;
using OficinaMecanica.Domain.Atendimento.Aggregates;
using OficinaMecanica.Domain.Atendimento.ValueObjects;

namespace OficinaMecanica.API.IntegrationTests.Atendimento.Controllers;

[Collection(OficinaMecanicaApiCollection.Nome)]
public sealed class ClientesInternosControllerTests : IAsyncLifetime
{
    private const string Rota = "/internal/v1/clientes/consultar-autenticacao";
    private const string TokenTeste = "credencial-tecnica-exclusiva-teste-clientes-2026";
    private readonly OficinaMecanicaApiFixture _fixture;

    public ClientesInternosControllerTests(OficinaMecanicaApiFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetarBancoAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient CriarCliente(bool habilitado = true, string tokenConfigurado = TokenTeste)
    {
        return _fixture.CreateClient(new Dictionary<string, string?>
        {
            ["Integracoes:ClientesInternos:Habilitado"] = habilitado.ToString(),
            ["Integracoes:ClientesInternos:Token"] = tokenConfigurado
        });
    }

    private async Task<Guid> CadastrarAsync(bool ativo)
    {
        var cliente = Cliente.Criar(CpfCnpj.Criar("52998224725"), "Cliente de teste",
            Endereco.Criar("Rua de teste", "10", "Centro", "São Paulo", "01001000"),
            Telefone.Criar("11999999999"), Email.Criar("teste@example.com"));
        if (!ativo)
        {
            cliente.Inativar();
        }
        await using var dbContext = _fixture.CriarDbContext();
        dbContext.Clientes.Add(cliente);
        await dbContext.SaveChangesAsync();
        return cliente.Id;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Dado_ClienteCadastrado_Quando_IdentidadeTecnicaConsultar_Entao_DeveRetornarSomenteIdEAtividade(bool ativo)
    {
        // Arrange
        var clienteId = await CadastrarAsync(ativo);
        using var client = CriarCliente();
        client.DefaultRequestHeaders.Add("X-Clientes-Internos-Token", TokenTeste);

        // Act
        using var response = await client.PostAsJsonAsync(Rota, new { documento = "529.982.247-25" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var dados = await response.Content.ReadFromJsonAsync<ConsultarClienteAutenticacaoResponse>();
        dados.Should().Be(new ConsultarClienteAutenticacaoResponse(clienteId, ativo));
        var corpo = await response.Content.ReadAsStringAsync();
        corpo.Should().NotContain("52998224725").And.NotContain("Cliente de teste").And.NotContain("teste@example.com");
    }

    [RequiresDockerFact]
    public async Task Dado_ClienteInexistente_Quando_Consultar_Entao_DeveDistinguirAusenciaDeRotaIndisponivel()
    {
        // Arrange
        using var client = CriarCliente();
        client.DefaultRequestHeaders.Add("X-Clientes-Internos-Token", TokenTeste);

        // Act
        using var response = await client.PostAsJsonAsync(Rota, new { documento = "52998224725" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ConsultarClienteAutenticacaoResponse>())
            .Should().Be(new ConsultarClienteAutenticacaoResponse(null, false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("token-incorreto")]
    public async Task Dado_CredencialTecnicaAusenteOuInvalida_Quando_Consultar_Entao_DeveNegar(string? token)
    {
        // Arrange
        await CadastrarAsync(true);
        using var client = CriarCliente();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Add("X-Clientes-Internos-Token", token);
        }

        // Act
        using var response = await client.PostAsJsonAsync(Rota, new { documento = "52998224725" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("52998224725");
    }

    [RequiresDockerFact]
    public async Task Dado_HeaderDuplicado_Quando_Consultar_Entao_DeveNegarMesmoComUmaCredencialCorreta()
    {
        // Arrange
        using var client = CriarCliente();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Clientes-Internos-Token", [TokenTeste, TokenTeste]);

        // Act
        using var response = await client.PostAsJsonAsync(Rota, new { documento = "52998224725" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RequiresDockerFact]
    public async Task Dado_ConsultaDesabilitada_Quando_Receber_Entao_NaoDeveRepresentarFalhaComoClienteAusente()
    {
        // Arrange
        using var client = CriarCliente(habilitado: false);
        client.DefaultRequestHeaders.Add("X-Clientes-Internos-Token", TokenTeste);

        // Act
        using var response = await client.PostAsJsonAsync(Rota, new { documento = "52998224725" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Theory]
    [InlineData("", HttpStatusCode.BadRequest)]
    [InlineData("invalido", HttpStatusCode.UnprocessableEntity)]
    public async Task Dado_DocumentoInvalido_Quando_IdentidadeTecnicaConsultar_Entao_DeveRejeitar(string documento, HttpStatusCode esperado)
    {
        // Arrange
        using var client = CriarCliente();
        client.DefaultRequestHeaders.Add("X-Clientes-Internos-Token", TokenTeste);

        // Act
        using var response = await client.PostAsJsonAsync(Rota, new { documento });

        // Assert
        response.StatusCode.Should().Be(esperado);
    }

    [RequiresDockerFact]
    public void Dado_TokenConfiguradoInvalido_Quando_Habilitar_Entao_DeveFalharSemExporValor()
    {
        // Arrange / Act
        var tentativa = () => CriarCliente(tokenConfigurado: "token-curto-nao-expor");

        // Assert
        tentativa.Should().Throw<OptionsValidationException>().Which.Message.Should().NotContain("token-curto-nao-expor");
    }
}
