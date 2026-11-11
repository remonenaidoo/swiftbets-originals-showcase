using System.Security.Cryptography;
using System.Text;

namespace SwiftBets.Originals.Infrastructure;

/// <summary>Lower-case hex HMAC-SHA256 of the exact request body, the same scheme the casino verifies.</summary>
public static class ProviderSignature
{
    public const string Header = "X-Provider-Signature";

    public static string Compute(string secret, ReadOnlySpan<byte> body)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        return Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));
    }
}
