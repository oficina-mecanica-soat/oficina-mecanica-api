using OficinaMecanica.Domain.Administrativo.Aggregates;
using OficinaMecanica.Domain.Administrativo.Enums;
using OficinaMecanica.Domain.Atendimento.Aggregates;
using OficinaMecanica.Domain.Atendimento.ValueObjects;

namespace OficinaMecanica.FluxoDistribuido.IntegrationTests.Factories;

internal sealed record DadosJornada(Cliente Cliente, Veiculo Veiculo, Mecanico Mecanico,
    ServicoCatalogo Servico, PecaInsumoCatalogo Peca, Guid FilialId);

internal static class DadosJornadaFactory
{
    public static DadosJornada Criar()
    {
        var cliente = Cliente.Criar(CpfCnpj.Criar("52998224725"), "Cliente de teste",
            Endereco.Criar("Rua de teste", "10", "Centro", "São Paulo", "01001000"),
            Telefone.Criar("11999999999"), Email.Criar("teste@example.com"));
        var veiculo = Veiculo.Criar(cliente.Id, Placa.Criar("ABC1D23"), "Marca", "Modelo", 2020);
        return new(cliente, veiculo, Mecanico.Criar("Mecânico de teste", "MEC-TESTE"),
            ServicoCatalogo.Criar("Troca de óleo", 150m),
            PecaInsumoCatalogo.Criar("Óleo", TipoPecaInsumo.INSUMO, 25m), Guid.NewGuid());
    }
}
