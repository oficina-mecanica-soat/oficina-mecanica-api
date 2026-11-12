namespace OficinaMecanica.Domain.GestaoOrdemServico.Messages;

public static class SagaOrdemServicoErrorMessages
{
    public const string IdentificadorObrigatorio = "Identificador da Saga ou do resultado é obrigatório.";
    public const string VersaoInvalida = "Versão de negócio da Saga inválida.";
    public const string TransicaoInvalida = "Transição da Saga inválida para o estado atual.";
    public const string ResultadoDivergente = "Resultado divergente da preparação ou do orçamento da Saga.";
    public const string ValorInvalido = "Valor do orçamento ou do pagamento inválido.";
    public const string DecisaoInvalida = "Decisão do orçamento inválida.";
    public const string CompensacaoAposInicioFisico = "Não é permitido compensar a preparação após o início físico da execução.";
    public const string DataFinalizacaoInvalida = "Data de finalização da execução deve ser informada em UTC.";
}
