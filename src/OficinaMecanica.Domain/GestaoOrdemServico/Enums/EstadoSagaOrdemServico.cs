namespace OficinaMecanica.Domain.GestaoOrdemServico.Enums;

public enum EstadoSagaOrdemServico
{
    Preparando = 1,
    AguardandoOrcamento = 2,
    AguardandoAprovacao = 3,
    ExecucaoAutorizada = 4,
    EmExecucao = 5,
    AguardandoPagamento = 6,
    PagamentoConfirmado = 7,
    CompensacaoPendente = 8,
    Compensada = 9,
    PreparacaoRecusada = 10,
    RecuperacaoPendente = 11
}
