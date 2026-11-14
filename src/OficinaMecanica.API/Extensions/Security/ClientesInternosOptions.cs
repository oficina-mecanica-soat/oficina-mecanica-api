using System.Text;

namespace OficinaMecanica.API.Extensions.Security;

public sealed class ClientesInternosOptions
{
    public const string SectionName = "Integracoes:ClientesInternos";
    public bool Habilitado { get; init; }
    public string Token { get; init; } = string.Empty;

    public bool PossuiTokenValido()
    {
        return !string.IsNullOrWhiteSpace(Token) && Encoding.UTF8.GetByteCount(Token) >= 32
            && Token.Length <= 256 && Token.All(caractere => caractere is >= '!' and <= '~');
    }
}
