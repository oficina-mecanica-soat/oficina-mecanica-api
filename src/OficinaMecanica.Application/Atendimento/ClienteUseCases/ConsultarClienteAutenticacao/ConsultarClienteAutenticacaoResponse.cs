namespace OficinaMecanica.Application.Atendimento.ClienteUseCases.ConsultarClienteAutenticacao;

public sealed record ConsultarClienteAutenticacaoResponse(Guid? ClienteId, bool Ativo);
