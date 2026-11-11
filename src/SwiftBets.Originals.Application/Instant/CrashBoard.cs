namespace SwiftBets.Originals.Application.Instant;

/// <summary>A bet in the shared round as every viewer sees it: a tag (not the bet's id), the masked player, the stake and its cash-out once made.</summary>
public sealed record BetPublic(Guid Table, string Tag, string Player, long Stake, long? CashedBp, long? Win);

/// <summary>The current shared round's bets, read by the loop, the HTTP handlers and every new viewer at once.</summary>
internal sealed class CrashBoard
{
    private readonly Dictionary<Guid, BetPublic> bets = [];
    private readonly Lock gate = new();
    private Guid table;

    public IReadOnlyList<BetPublic> NewestFirst()
    {
        lock (gate)
        {
            return [.. bets.Values.Reverse()];
        }
    }

    public void Reset(Guid newTable)
    {
        lock (gate)
        {
            table = newTable;
            bets.Clear();
        }
    }

    /// <summary>No round is live: later cash-outs are not shown, the last round's bets stay.</summary>
    public void Detach()
    {
        lock (gate)
        {
            table = Guid.Empty;
        }
    }

    /// <summary>Shows the bet unless the board has moved on to another round.</summary>
    public void Show(Guid roundId, BetPublic bet)
    {
        lock (gate)
        {
            if (bet.Table == table)
            {
                bets[roundId] = bet;
            }
        }
    }
}
