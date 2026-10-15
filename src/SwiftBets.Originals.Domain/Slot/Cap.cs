namespace SwiftBets.Originals.Domain.Slot;

/// <summary>The max-win cap, applied per round (a base spin and the feature it triggers).</summary>
public static class Cap
{
    /// <summary>How much of <paramref name="winUnits"/> may be paid, and whether this payment reaches the cap.</summary>
    public static (long Paid, bool Reached) Apply(long paidSoFarUnits, long winUnits, long capUnits)
    {
        var room = Math.Max(0, capUnits - paidSoFarUnits);
        var paid = Math.Min(winUnits, room);
        return (paid, paidSoFarUnits + paid >= capUnits);
    }
}
