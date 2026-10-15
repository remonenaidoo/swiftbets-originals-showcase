namespace SwiftBets.Originals.Domain.Slot;

/// <summary>
/// Everything that decides a slot's outcomes and pays. Nothing else may: the PAR sheet is generated from this,
/// and its hash is recorded in the sheet so a change without a regenerated sheet fails the build.
/// </summary>
/// <param name="GameId">The catalogue id.</param>
/// <param name="BaseReels">Five strips for the base game; a spin draws one stop per strip.</param>
/// <param name="FreeReels">Five strips used during free spins.</param>
/// <param name="Lines">Paylines, each five row indexes (0 top, 2 bottom), read left to right.</param>
/// <param name="LinePays">Pays for 3, 4 and 5 of a kind, in line-bet units.</param>
/// <param name="ScatterPays">Pays for 3, 4 and 5 Scatters anywhere, in total-stake units.</param>
/// <param name="FreeSpinsAwarded">Spins awarded by 3+ Scatters, in the base game and again on a retrigger.</param>
/// <param name="FreeSpinMultiplier">Every free-spin win is multiplied by this.</param>
/// <param name="MaxWinStakes">The most one round (a base spin and its feature) can pay, in total stakes.</param>
public sealed record GameDefinition(
    string GameId,
    IReadOnlyList<IReadOnlyList<Symbol>> BaseReels,
    IReadOnlyList<IReadOnlyList<Symbol>> FreeReels,
    IReadOnlyList<int[]> Lines,
    IReadOnlyDictionary<Symbol, int[]> LinePays,
    int[] ScatterPays,
    int FreeSpinsAwarded,
    int FreeSpinMultiplier,
    int MaxWinStakes)
{
    public const int Rows = 3;

    public int LineCount => Lines.Count;

    /// <summary>The cap in line-bet units: <see cref="MaxWinStakes"/> total stakes.</summary>
    public long CapUnits => (long)MaxWinStakes * LineCount;
}
