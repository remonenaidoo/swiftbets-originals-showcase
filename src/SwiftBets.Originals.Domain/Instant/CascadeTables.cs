namespace SwiftBets.Originals.Domain.Instant;

/// <summary>
/// Kalahari Gem Fall's strips (symbol counts, shuffled per reel), pays (units; a stake is 100) and meteor values,
/// tuned by simulation to 95.96% ± 0.12% (2e8 rounds) and fixed here; the maths document measures 1e9 rounds.
/// </summary>
public static class CascadeTables
{
    public static CascadeConfig Config { get; } = new(
        BaseCounts: new Dictionary<string, int> { ["Sunstone"] = 3, ["Tanzanite"] = 4, ["TigersEye"] = 6, ["DesertRose"] = 8, ["Quartz"] = 12, ["Agate"] = 14, ["Jasper"] = 16, ["Amethyst"] = 17, ["Opal"] = 18, ["Sun"] = 2 },
        FreeCounts: new Dictionary<string, int> { ["Sunstone"] = 3, ["Tanzanite"] = 4, ["TigersEye"] = 6, ["DesertRose"] = 8, ["Quartz"] = 12, ["Agate"] = 14, ["Jasper"] = 16, ["Amethyst"] = 17, ["Opal"] = 18, ["Sun"] = 2, ["Meteor"] = 3 },
        Pays: new Dictionary<string, int[]>
        {
            ["Sunstone"] = [1235, 3090, 6175],
            ["Tanzanite"] = [310, 1235, 3090],
            ["TigersEye"] = [245, 620, 1855],
            ["DesertRose"] = [185, 245, 1480],
            ["Quartz"] = [123, 185, 1235],
            ["Agate"] = [100, 150, 990],
            ["Jasper"] = [60, 125, 620],
            ["Amethyst"] = [50, 110, 495],
            ["Opal"] = [30, 95, 245],
        },
        SunStakes: [3, 5, 100],
        Meteors: [(2, 400), (3, 250), (5, 150), (10, 100), (25, 70), (50, 30)]);
}
