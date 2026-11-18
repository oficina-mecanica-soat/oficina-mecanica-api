namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;

public sealed record PreparacaoCanceladaDados(
    Guid PreparacaoId,
    int Versao);
