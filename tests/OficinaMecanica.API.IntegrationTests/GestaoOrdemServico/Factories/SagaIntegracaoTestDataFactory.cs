using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.SolicitarOrcamentoSaga;
using OficinaMecanica.Domain.Administrativo.Aggregates;
using OficinaMecanica.Domain.Atendimento.Aggregates;
using OficinaMecanica.Domain.Atendimento.ValueObjects;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;
using OficinaMecanica.Infrastructure.Persistence;
namespace OficinaMecanica.API.IntegrationTests.GestaoOrdemServico.Factories;

internal static class SagaIntegracaoTestDataFactory
{
    public static async Task<SolicitarOrcamentoSagaRequest> CriarAsync(OficinaMecanicaDbContext contexto, bool criarSaga = true)
    {
        var cliente = Cliente.Criar(CpfCnpj.Criar("52998224725"), "Cliente de teste",
            Endereco.Criar("Rua de teste", "10", "Centro", "São Paulo", "01001000"), Telefone.Criar("11999999999"), Email.Criar("teste@example.com"));
        var veiculo = Veiculo.Criar(cliente.Id, Placa.Criar("ABC1D23"), "Marca", "Modelo", 2020);
        var mecanico = Mecanico.Criar("Mecânico de teste", "MEC-TESTE");
        var catalogo = ServicoCatalogo.Criar("Troca de óleo", 150m);
        var ordem = OrdemServico.Abrir(1, veiculo.Id, mecanico.Id);
        ordem.RegistrarServicoNaAbertura(catalogo.Id, 150m);
        if (criarSaga) { ordem.IniciarDiagnostico(); }
        var saga = SagaOrdemServico.Iniciar(ordem.Id, 2, 3);
        saga.RegistrarPreparacao(Guid.NewGuid(), 2);
        contexto.AddRange(cliente, veiculo, mecanico, catalogo, ordem);
        if (criarSaga) { contexto.Add(saga); }
        await contexto.SaveChangesAsync();
        var servico = ordem.Servicos.Single();
        return new(saga.Id, ordem.Id, Guid.NewGuid(), "correlacao-integracao",
            new(3, cliente.Id, [new(servico.Id, catalogo.Id, catalogo.Descricao, servico.Valor)], []));
    }
}
