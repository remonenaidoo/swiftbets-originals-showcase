using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application;

/// <summary>What the spin and feature handlers share: stakes, money conversion, the player reference, wallet codes.</summary>
internal static class Play
{
    public static readonly long[] Stakes = [100, 200, 500, 1000, 2000, 5000];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Line-bet units to minor units: the stake is always a multiple of the line count.</summary>
    public static long Money(long units, long stake, int lines) => units * (stake / lines);

    public static string BetTx(Guid round) => $"{round}:bet";

    public static string WinTx(Guid round, int seq) => $"{round}:win:{seq}";

    public static string RollbackTx(Guid round) => $"{round}:rollback";

    public static string OutcomeJson(SpinOutcome outcome) =>
        JsonSerializer.Serialize(new { lines = outcome.Lines, scatters = outcome.Scatters, scatterUnits = outcome.ScatterUnits, totalUnits = outcome.TotalUnits, trigger = outcome.TriggersFeature }, Json);

    public static IReadOnlyList<LineWin> LinesOf(string outcomeJson) =>
        JsonDocument.Parse(outcomeJson).RootElement.TryGetProperty("lines", out var lines)
            ? lines.Deserialize<List<LineWin>>(Json) ?? []
            : [];

    public static int ScattersOf(string outcomeJson) =>
        JsonDocument.Parse(outcomeJson).RootElement.TryGetProperty("scatters", out var s) ? s.GetInt32() : 0;

    /// <summary>Who is playing, from the casino. Without the casino's reference the round is bound to this session only.</summary>
    public static async Task<(string? PlayerRef, long? Balance, SpinError? Error)> IdentifyAsync(IWallet wallet, string sessionToken, CancellationToken cancellationToken)
    {
        var reply = await wallet.BalanceAsync(sessionToken, cancellationToken);
        return reply.Status switch
        {
            WalletStatus.Ok => (reply.PlayerRef ?? $"session:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sessionToken)))}", reply.Balance, null),
            WalletStatus.SessionInvalid => (null, null, SpinError.SessionInvalid),
            _ => (null, null, SpinError.WalletUnavailable),
        };
    }

    public static SpinError Refusal(string status) => status switch
    {
        WalletStatus.InsufficientFunds => SpinError.InsufficientFunds,
        WalletStatus.LimitExceeded => SpinError.LimitExceeded,
        WalletStatus.AccountRestricted => SpinError.AccountRestricted,
        WalletStatus.SessionInvalid => SpinError.SessionInvalid,
        _ => SpinError.WalletUnavailable,
    };

    public static SpinView View(RoundRecord round, SpinRecord spin, GameDefinition game, long? balance) => new(
        round.RoundId,
        spin.Seq,
        spin.Stops,
        LinesOf(spin.OutcomeJson),
        ScattersOf(spin.OutcomeJson),
        spin.Win,
        Money(round.PaidUnits, round.Stake, game.LineCount),
        round.FreeSpinsLeft,
        spin.Seq == 0 && ScattersOf(spin.OutcomeJson) >= 3,
        round.PaidUnits >= game.CapUnits,
        balance);
}
