using System.Text.Json.Serialization;

namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;

public sealed record CancelarOrcamentoDados(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OrcamentoId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Versao,
    string Motivo);
