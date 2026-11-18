using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OficinaMecanica.API.Extensions.Responses;
using OficinaMecanica.API.Extensions.Security;
using OficinaMecanica.Application.Atendimento.ClienteUseCases.ConsultarClienteAutenticacao;

namespace OficinaMecanica.API.Atendimento.Controllers;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/v1/clientes")]
[ClientesInternosAuthorize]
public sealed class ClientesInternosController : ControllerBase
{
    [HttpPost("consultar-autenticacao")]
    public async Task<IActionResult> ConsultarAutenticacao([FromServices] ConsultarClienteAutenticacaoUseCase useCase,
        [FromBody] ConsultarClienteAutenticacaoRequest request, CancellationToken cancellationToken)
    {
        return this.ToActionResult(await useCase.ExecuteAsync(request, cancellationToken));
    }
}
