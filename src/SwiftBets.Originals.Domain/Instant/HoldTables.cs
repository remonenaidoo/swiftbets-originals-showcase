namespace SwiftBets.Originals.Domain.Instant;

/// <summary>Highveld Hold's strips, pays, coin values and respin chance, tuned once (exact RTP 95.99%) and fixed here.</summary>
public static partial class HoldTables
{
    private static readonly string[][] ReelNames =
    [
        ["Coin", "Coin", "Coin", "Cosmos", "Amber", "Springbok", "Emerald", "Amber", "Ruby", "Cosmos", "Emerald", "Springbok", "Ruby", "Amber", "Sapphire", "Cosmos", "Sapphire", "Emerald", "Springbok", "Ruby", "Amber", "Cosmos", "Windpomp", "Windpomp", "Thunderbird", "Springbok", "Emerald", "Coin", "Cosmos", "Amber", "Ruby", "Sapphire", "Springbok", "Cosmos", "Amber", "Emerald", "Ruby", "Windpomp", "Sapphire", "Cosmos", "Springbok", "Amber", "Emerald", "Ruby", "Thunderbird", "Cosmos", "Springbok", "Thunderbird", "Amber", "Emerald", "Ruby", "Cosmos", "Sapphire", "Springbok", "Windpomp"],
        ["Coin", "Coin", "Coin", "Amber", "Springbok", "Emerald", "Ruby", "Amber", "Thunderbird", "Wild", "Springbok", "Emerald", "Wild", "Amber", "Ruby", "Windpomp", "Springbok", "Thunderbird", "Windpomp", "Cosmos", "Amber", "Emerald", "Springbok", "Wild", "Cosmos", "Thunderbird", "Ruby", "Amber", "Springbok", "Emerald", "Windpomp", "Sapphire", "Sapphire", "Sapphire", "Springbok", "Amber", "Thunderbird", "Ruby", "Emerald", "Wild", "Windpomp", "Springbok", "Amber", "Cosmos", "Thunderbird", "Wild", "Sapphire", "Springbok", "Amber", "Emerald", "Ruby", "Windpomp", "Cosmos", "Springbok", "Thunderbird"],
        ["Coin", "Coin", "Coin", "Amber", "Springbok", "Emerald", "Ruby", "Amber", "Thunderbird", "Wild", "Springbok", "Emerald", "Wild", "Amber", "Ruby", "Windpomp", "Springbok", "Thunderbird", "Windpomp", "Cosmos", "Amber", "Emerald", "Springbok", "Wild", "Cosmos", "Thunderbird", "Ruby", "Amber", "Springbok", "Emerald", "Windpomp", "Sapphire", "Sapphire", "Sapphire", "Springbok", "Amber", "Thunderbird", "Ruby", "Emerald", "Wild", "Windpomp", "Springbok", "Amber", "Cosmos", "Thunderbird", "Wild", "Sapphire", "Springbok", "Amber", "Emerald", "Ruby", "Windpomp", "Cosmos", "Springbok", "Thunderbird"],
        ["Coin", "Coin", "Coin", "Amber", "Springbok", "Emerald", "Ruby", "Amber", "Thunderbird", "Wild", "Springbok", "Emerald", "Wild", "Amber", "Ruby", "Windpomp", "Springbok", "Thunderbird", "Windpomp", "Cosmos", "Amber", "Emerald", "Springbok", "Wild", "Cosmos", "Thunderbird", "Ruby", "Amber", "Springbok", "Emerald", "Windpomp", "Sapphire", "Sapphire", "Sapphire", "Springbok", "Amber", "Thunderbird", "Ruby", "Emerald", "Wild", "Windpomp", "Springbok", "Amber", "Cosmos", "Thunderbird", "Wild", "Sapphire", "Springbok", "Amber", "Emerald", "Ruby", "Windpomp", "Cosmos", "Springbok", "Thunderbird"],
        ["Coin", "Coin", "Coin", "Cosmos", "Amber", "Springbok", "Emerald", "Amber", "Ruby", "Cosmos", "Emerald", "Springbok", "Ruby", "Amber", "Sapphire", "Cosmos", "Sapphire", "Emerald", "Springbok", "Ruby", "Amber", "Cosmos", "Windpomp", "Windpomp", "Thunderbird", "Springbok", "Emerald", "Coin", "Cosmos", "Amber", "Ruby", "Sapphire", "Springbok", "Cosmos", "Amber", "Emerald", "Ruby", "Windpomp", "Sapphire", "Cosmos", "Springbok", "Amber", "Emerald", "Ruby", "Thunderbird", "Cosmos", "Springbok", "Thunderbird", "Amber", "Emerald", "Ruby", "Cosmos", "Sapphire", "Springbok", "Windpomp"],
    ];

    /// <summary>Line pays for 3, 4 and 5 of a kind, in line bets (a stake is 20 line bets).</summary>
    public static readonly IReadOnlyDictionary<string, int[]> Pays = new Dictionary<string, int[]>
    {
        ["Thunderbird"] = [25, 100, 500],
        ["Windpomp"] = [20, 75, 300],
        ["Springbok"] = [15, 50, 200],
        ["Cosmos"] = [10, 40, 150],
        ["Ruby"] = [5, 15, 60],
        ["Sapphire"] = [5, 15, 60],
        ["Emerald"] = [4, 10, 40],
        ["Amber"] = [4, 10, 40],
    };

    /// <summary>Coin values in stakes, with their weights out of 1,000. 15, 40 and 150 are the Mini, Minor and Major.</summary>
    public static readonly (int Stakes, int Weight)[] Values = [(1, 390), (2, 250), (3, 150), (5, 100), (10, 60), (15, 22), (25, 15), (40, 9), (150, 4)];

    /// <summary>The chance, per respin, that an empty cell lands a coin, in basis points (651 = 6.51%).</summary>
    public const int RespinChanceBp = 651;
}
