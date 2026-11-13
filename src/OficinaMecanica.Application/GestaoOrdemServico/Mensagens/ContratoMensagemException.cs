namespace OficinaMecanica.Application.GestaoOrdemServico.Mensagens;

public sealed class ContratoMensagemException : Exception
{
    public ContratoMensagemException(string codigo) : base(codigo) { }
}
