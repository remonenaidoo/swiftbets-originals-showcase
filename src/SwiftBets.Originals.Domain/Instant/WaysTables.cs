namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Cape Point Ways' strips and pays, tuned once (exact RTP 95.99%) and fixed here; the maths tool proves them.</summary>
public static partial class WaysTables
{
    private static readonly string[][] BaseNames =
    [
        ["Whale", "Anchor", "Compass", "Shell", "Starfish", "Penguin", "Compass", "Anchor", "Lighthouse", "Shark", "Pearl", "Compass", "Anchor", "Starfish", "Penguin", "Shell", "Compass", "Lighthouse", "Anchor", "Starfish", "Compass", "Shell", "Shark", "Anchor", "Penguin", "Compass", "Starfish", "Lighthouse"],
        ["Pearl", "Anchor", "Shell", "Whale", "Starfish", "Compass", "Penguin", "Anchor", "Shell", "Lighthouse", "Starfish", "Wild", "Whale", "Compass", "Anchor", "Shell", "Wild", "Starfish", "Penguin", "Shark", "Shark", "Anchor", "Shell", "Starfish", "Compass", "Lighthouse", "Whale", "Anchor", "Shell", "Penguin", "Starfish", "Wild", "Compass", "Lighthouse", "Anchor", "Shell", "Starfish", "Whale", "Wild", "Shark", "Anchor", "Shell", "Compass", "Starfish", "Lighthouse", "Penguin"],
        ["Pearl", "Anchor", "Shell", "Whale", "Starfish", "Compass", "Penguin", "Anchor", "Shell", "Lighthouse", "Starfish", "Wild", "Whale", "Compass", "Anchor", "Shell", "Wild", "Starfish", "Penguin", "Shark", "Shark", "Anchor", "Shell", "Starfish", "Compass", "Lighthouse", "Whale", "Anchor", "Shell", "Penguin", "Starfish", "Wild", "Compass", "Lighthouse", "Anchor", "Shell", "Starfish", "Whale", "Wild", "Shark", "Anchor", "Shell", "Compass", "Starfish", "Lighthouse", "Penguin"],
        ["Pearl", "Anchor", "Shell", "Whale", "Starfish", "Compass", "Penguin", "Anchor", "Shell", "Lighthouse", "Starfish", "Wild", "Whale", "Compass", "Anchor", "Shell", "Wild", "Starfish", "Penguin", "Shark", "Shark", "Anchor", "Shell", "Starfish", "Compass", "Lighthouse", "Whale", "Anchor", "Shell", "Penguin", "Starfish", "Wild", "Compass", "Lighthouse", "Anchor", "Shell", "Starfish", "Whale", "Wild", "Shark", "Anchor", "Shell", "Compass", "Starfish", "Lighthouse", "Penguin"],
        ["Whale", "Anchor", "Compass", "Shell", "Starfish", "Penguin", "Compass", "Anchor", "Lighthouse", "Shark", "Pearl", "Compass", "Anchor", "Starfish", "Penguin", "Shell", "Compass", "Lighthouse", "Anchor", "Starfish", "Compass", "Shell", "Shark", "Anchor", "Penguin", "Compass", "Starfish", "Lighthouse"],
    ];

    private static readonly string[][] FreeNames =
    [
        ["Whale", "Anchor", "Compass", "Shell", "Penguin", "Shark", "Whale", "Starfish", "Shell", "Compass", "Anchor", "Shark", "Lighthouse", "Whale", "Penguin", "Compass", "Anchor", "Shell", "Lighthouse", "Whale", "Shark", "Compass", "Penguin", "Anchor", "Shell", "Whale", "Starfish", "Compass", "Pearl", "Shark", "Pearl", "Whale", "Anchor", "Compass", "Shell", "Penguin", "Starfish"],
        ["Wild3", "Shell", "Anchor", "Starfish", "Wild", "Shark", "Compass", "Shell", "Wild2", "Starfish", "Anchor", "Wild", "Lighthouse", "Penguin", "Starfish", "Shell", "Whale", "Anchor", "Pearl", "Compass", "Starfish", "Shell", "Wild", "Lighthouse", "Wild2", "Starfish", "Anchor", "Shell", "Penguin", "Shark", "Wild", "Starfish", "Anchor", "Shell", "Compass", "Wild2", "Starfish", "Lighthouse", "Whale"],
        ["Wild3", "Shell", "Anchor", "Starfish", "Wild", "Shark", "Compass", "Shell", "Wild2", "Starfish", "Anchor", "Wild", "Lighthouse", "Penguin", "Starfish", "Shell", "Whale", "Anchor", "Pearl", "Compass", "Starfish", "Shell", "Wild", "Lighthouse", "Wild2", "Starfish", "Anchor", "Shell", "Penguin", "Shark", "Wild", "Starfish", "Anchor", "Shell", "Compass", "Wild2", "Starfish", "Lighthouse", "Whale"],
        ["Wild3", "Shell", "Anchor", "Starfish", "Wild", "Shark", "Compass", "Shell", "Wild2", "Starfish", "Anchor", "Wild", "Lighthouse", "Penguin", "Starfish", "Shell", "Whale", "Anchor", "Pearl", "Compass", "Starfish", "Shell", "Wild", "Lighthouse", "Wild2", "Starfish", "Anchor", "Shell", "Penguin", "Shark", "Wild", "Starfish", "Anchor", "Shell", "Compass", "Wild2", "Starfish", "Lighthouse", "Whale"],
        ["Whale", "Anchor", "Compass", "Shell", "Penguin", "Shark", "Whale", "Starfish", "Shell", "Compass", "Anchor", "Shark", "Lighthouse", "Whale", "Penguin", "Compass", "Anchor", "Shell", "Lighthouse", "Whale", "Shark", "Compass", "Penguin", "Anchor", "Shell", "Whale", "Starfish", "Compass", "Pearl", "Shark", "Pearl", "Whale", "Anchor", "Compass", "Shell", "Penguin", "Starfish"],
    ];

    /// <summary>Pays per way for 3, 4 and 5 of a kind, in units (a stake is 20 units).</summary>
    public static readonly IReadOnlyDictionary<string, int[]> Pays = new Dictionary<string, int[]>
    {
        ["Shark"] = [10, 40, 152],
        ["Whale"] = [8, 30, 100],
        ["Lighthouse"] = [6, 20, 70],
        ["Penguin"] = [5, 15, 50],
        ["Anchor"] = [2, 6, 20],
        ["Compass"] = [2, 6, 20],
        ["Shell"] = [1, 4, 15],
        ["Starfish"] = [1, 4, 15],
    };
}
