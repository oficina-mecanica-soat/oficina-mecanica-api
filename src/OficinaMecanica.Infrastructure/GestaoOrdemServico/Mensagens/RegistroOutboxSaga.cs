using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;

namespace OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;

public sealed class RegistroOutboxSaga
{
    public Guid Id
    { get; init; }

    public Guid OrdemServicoId
    { get; init; }

    public int VersaoNegocio
    { get; init; }

    public string Tipo { get; init; } = string.Empty;
    public string Destino { get; init; } = string.Empty;
    public string Corpo { get; init; } = string.Empty;
    public DateTimeOffset CriadoEm
    { get; init; }

    public DateTimeOffset? PublicadoEm
    { get; set; }

    public Guid? Responsavel
    { get; set; }

    public DateTimeOffset? LeaseAte
    { get; set; }

    public int Tentativas
    { get; set; }

    public DateTimeOffset? ProximaTentativaEm
    { get; set; }

    public static RegistroOutboxSaga Criar(MensagemIntegracao mensagem, int versao, string destino) => new()
    {
        Id = mensagem.MensagemId,
        OrdemServicoId = mensagem.OrdemServicoId,
        VersaoNegocio = versao,
        Tipo = mensagem.Tipo,
        Destino = destino,
        Corpo = JsonContrato.Serializar(mensagem),
        CriadoEm = mensagem.OcorridoEm
    };
}
