using System.Security.Cryptography;
using System.Text;

namespace SwiftBets.Originals.Infrastructure;

/// <summary>
/// How the casino authenticates a provider: lower-case hex HMAC-SHA256 of the exact request body with the shared
/// secret, in <c>X-Provider-Signature</c>. The same scheme as the casino's own <c>ProviderSignature</c>.
/// </summary>
public static class ProviderSignature
{
    public const string Header = "X-Provider-Signature";

    public static string Compute(string secret, ReadOnlySpan<byte> body)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        return Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));
    }
}
