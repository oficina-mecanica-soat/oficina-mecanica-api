namespace OficinaMecanica.Application.GestaoOrdemServico.ValidationMessages;

public static class SagaIntegracaoValidationMessages
{
    public const string SagaObrigatoria = "Identificador da Saga é obrigatório.";
    public const string OrdemObrigatoria = "Ordem de serviço é obrigatória.";
    public const string OperacaoObrigatoria = "Identificador da operação é obrigatório.";
    public const string CorrelacaoInvalida = "Correlação inválida.";
    public const string SnapshotObrigatorio = "Snapshot do orçamento é obrigatório.";
    public const string ClienteObrigatorio = "Cliente é obrigatório.";
    public const string VersaoInvalida = "Versão deve ser maior que zero.";
    public const string ServicosObrigatorios = "Informe ao menos um serviço.";
    public const string ItensObrigatorios = "Coleção de peças e insumos é obrigatória.";
    public const string ServicosInvalidos = "Snapshot de serviços inválido.";
    public const string ItensInvalidos = "Snapshot de peças e insumos inválido.";
    public const string TotalInvalido = "Total do orçamento deve ser representável com precisão SQL 18,2.";
    public const string SagaNaoEncontrada = "Saga não encontrada.";
    public const string ServicoIdObrigatorio = "Identificador da instância do serviço é obrigatório.";
    public const string ServicoCatalogoObrigatorio = "Referência do serviço ao catálogo é obrigatória.";
    public const string ItemIdObrigatorio = "Referência da peça ou insumo é obrigatória.";
    public const string DescricaoInvalida = "Descrição deve possuir entre 1 e 160 caracteres.";
    public const string QuantidadeInvalida = "Quantidade deve ser maior que zero.";
    public const string ValorInvalido = "Valor deve ser positivo e representável com precisão SQL 18,2.";
}
