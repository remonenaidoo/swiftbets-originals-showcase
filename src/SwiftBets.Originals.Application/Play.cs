using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application;

/// <summary>What the spin and feature handlers share: money conversion, stored outcomes, the player reference, wallet codes.</summary>
internal static class Play
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Line-bet units to minor units: every offered stake is a multiple of the line count.</summary>
    public static long Money(long units, long stake, int lines) => units * (stake / lines);

    public static string BetTx(Guid round) => $"{round}:bet";

    public static string WinTx(Guid round, int seq) => $"{round}:win:{seq}";

    public static string RollbackTx(Guid round) => $"{round}:rollback";

    /// <summary>The stored outcome: symbols by name, so a spin replays whatever the symbol table's order becomes.</summary>
    public static string OutcomeJson(GameDefinition game, SpinOutcome outcome) => JsonSerializer.Serialize(new Outcome(
        [.. outcome.Lines.Select(l => new LineView(l.Line, game.Name(l.Symbol), l.Count, l.Reels, l.Units))],
        outcome.Scatters,
        outcome.ScatterUnits,
        outcome.TotalUnits,
        outcome.TriggersFeature,
        outcome.Locked.Count == 0 ? null : [.. outcome.Locked.Select(c => new LockView(c.Reel, c.Row, game.Name(c.Symbol)))]), Json);

    /// <summary>Reads a stored outcome. Rounds stored before split symbols have no <c>reels</c>: their count is their reels.</summary>
    public static Outcome Read(string outcomeJson)
    {
        var o = JsonSerializer.Deserialize<Outcome>(outcomeJson, Json)!;
        return o with { Lines = [.. (o.Lines ?? []).Select(l => l.Reels == 0 ? l with { Reels = l.Count } : l)] };
    }

    public static IReadOnlyList<LockedCell> LocksOf(GameDefinition game, string outcomeJson) =>
        [.. (Read(outcomeJson).Locked ?? []).Select(l => new LockedCell(l.Reel, l.Row, game.Id(l.Symbol)))];

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

    public static SpinView View(RoundRecord round, SpinRecord spin, GameDefinition game, long? balance)
    {
        var outcome = Read(spin.OutcomeJson);
        return new SpinView(
            round.RoundId,
            spin.Seq,
            spin.Stops,
            outcome.Lines,
            outcome.Scatters,
            spin.Win,
            Money(round.PaidUnits, round.Stake, game.LineCount),
            round.FreeSpinsLeft,
            spin.Seq == 0 && outcome.Trigger,
            round.PaidUnits >= game.CapUnits,
            balance,
            outcome.Locked ?? []);
    }

    /// <summary>A stored spin outcome.</summary>
    public sealed record Outcome(IReadOnlyList<LineView> Lines, int Scatters, long ScatterUnits, long TotalUnits, bool Trigger, IReadOnlyList<LockView>? Locked);
}
