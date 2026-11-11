using System.Text.Json;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Application;

internal static class SlotSpins
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Line-bet units as money at the game's rate, floored to the cent; every offered stake divides by the line count.</summary>
    public static long Money(long units, long stake, int lines, long rateBp) => (long)((Int128)units * (stake / lines) * rateBp / Multipliers.Base);

    /// <summary>Symbols are stored by name, so a stored spin survives a reordered symbol table.</summary>
    public static string OutcomeJson(GameDefinition game, SpinOutcome outcome) => JsonSerializer.Serialize(new StoredOutcome(
        [.. outcome.Lines.Select(l => new LineView(l.Line, game.Name(l.Symbol), l.Count, l.Reels, l.Units))],
        outcome.Scatters,
        outcome.ScatterUnits,
        outcome.TotalUnits,
        outcome.TriggersFeature,
        outcome.Locked.Count == 0 ? null : [.. outcome.Locked.Select(c => new LockView(c.Reel, c.Row, game.Name(c.Symbol)))]), Json);

    public static IReadOnlyList<LockedCell> LocksOf(GameDefinition game, string outcomeJson) =>
        [.. (Read(outcomeJson).Locked ?? []).Select(l => new LockedCell(l.Reel, l.Row, game.Id(l.Symbol)))];

    public static SpinView View(RoundRecord round, SpinRecord spin, GameDefinition game, long? balance, long rateBp)
    {
        var outcome = Read(spin.OutcomeJson);
        return new SpinView(
            round.RoundId,
            spin.Seq,
            spin.Stops,
            outcome.Lines,
            outcome.Scatters,
            spin.Win,
            Money(round.PaidUnits, round.Stake, game.LineCount, rateBp),
            round.FreeSpinsLeft,
            spin.Seq == 0 && outcome.Trigger,
            round.PaidUnits >= game.CapUnits,
            balance,
            outcome.Locked ?? []);
    }

    private static StoredOutcome Read(string outcomeJson)
    {
        var o = JsonSerializer.Deserialize<StoredOutcome>(outcomeJson, Json)!;

        // Spins stored before split symbols have no reels: their count is their reels.
        return o with { Lines = [.. (o.Lines ?? []).Select(l => l.Reels == 0 ? l with { Reels = l.Count } : l)] };
    }

    private sealed record StoredOutcome(IReadOnlyList<LineView> Lines, int Scatters, long ScatterUnits, long TotalUnits, bool Trigger, IReadOnlyList<LockView>? Locked);
}
