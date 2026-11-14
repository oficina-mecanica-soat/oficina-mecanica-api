using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using OficinaMecanica.API.Responses;
using OficinaMecanica.Application.Common;

namespace OficinaMecanica.API.Extensions.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ClientesInternosAuthorizeAttribute : TypeFilterAttribute
{
    public ClientesInternosAuthorizeAttribute() : base(typeof(ClientesInternosAuthorizationFilter))
    {
    }
}

internal sealed class ClientesInternosAuthorizationFilter : IAuthorizationFilter
{
    public const string HeaderName = "X-Clientes-Internos-Token";
    private readonly ClientesInternosOptions _options;

    public ClientesInternosAuthorizationFilter(IOptions<ClientesInternosOptions> options)
    {
        _options = options.Value;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        if (!_options.Habilitado || !_options.PossuiTokenValido())
        {
            context.Result = new ObjectResult(new ErrorResponse(ApiResponseMessages.ServicoTemporariamenteIndisponivel, TipoErro.ErroInterno))
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
            return;
        }
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var token) || token.Count != 1 || !TokensMatch(token[0]))
        {
            context.Result = new UnauthorizedObjectResult(new ErrorResponse(ApiResponseMessages.NaoAutorizado, TipoErro.NaoAutorizado));
        }
    }

    private bool TokensMatch(string? token)
    {
        var esperado = Encoding.UTF8.GetBytes(_options.Token);
        var recebido = Encoding.UTF8.GetBytes(token ?? string.Empty);
        return esperado.Length == recebido.Length && CryptographicOperations.FixedTimeEquals(esperado, recebido);
    }
}
