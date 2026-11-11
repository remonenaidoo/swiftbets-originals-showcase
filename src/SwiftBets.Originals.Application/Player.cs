using System.Security.Cryptography;
using System.Text;
using SwiftBets.Originals.Application.Ports;

namespace SwiftBets.Originals.Application;

internal sealed record Player(string Ref, long? Balance);

internal static class WalletIdentity
{
    /// <summary>Who is playing, per the casino; without its player reference the round is bound to this session only.</summary>
    public static async Task<Result<Player>> IdentifyAsync(this IWallet wallet, string sessionToken, CancellationToken cancellationToken)
    {
        var reply = await wallet.BalanceAsync(sessionToken, cancellationToken);
        return reply.Status switch
        {
            WalletStatus.Ok => new Player(reply.PlayerRef ?? SessionRef(sessionToken), reply.Balance),
            WalletStatus.SessionInvalid => GameError.SessionInvalid,
            _ => GameError.WalletUnavailable,
        };
    }

    private static string SessionRef(string sessionToken) => $"session:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sessionToken)))}";
}
