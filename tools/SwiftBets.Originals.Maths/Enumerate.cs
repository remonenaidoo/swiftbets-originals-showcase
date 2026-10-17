using System.Numerics;
using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Maths;

/// <summary>Totals over every combination of stops (the full cycle) for one set of reels.</summary>
public sealed record ReelStats(
    BigInteger Cycle,
    BigInteger TotalUnits,
    BigInteger Hits,
    BigInteger Triggers,
    BigInteger RetriggerSpinsAwarded,
    IReadOnlyDictionary<string, BigInteger> UnitsBySymbolAndCount,
    IReadOnlyDictionary<long, BigInteger> CountByWinUnits)
{
    public double Mean(BigInteger units) => (double)units / (double)Cycle;
}

/// <summary>
/// Full-cycle enumeration: every stop combination is evaluated once by the production evaluator, and wins are
/// summed as exact integers (line-bet units), so the RTP is an exact rational. Parallel over reel 1's stops,
/// one accumulator per worker, merged at the end.
/// </summary>
public static class Enumerate
{
    public static ReelStats Run(GameDefinition game, IReadOnlyList<IReadOnlyList<byte>> reels, int multiplier)
    {
        var lengths = reels.Select(r => r.Count).ToArray();
        var parts = new Accumulator[lengths[0]];
        Parallel.For(0, lengths[0], first =>
        {
            var acc = new Accumulator(game.Symbols.Count);
            var stops = new int[5];
            var window = new byte[5, GameDefinition.Rows];
            stops[0] = first;
            Fill(window, reels, 0, first);
            for (stops[1] = 0; stops[1] < lengths[1]; stops[1]++)
            {
                Fill(window, reels, 1, stops[1]);
                for (stops[2] = 0; stops[2] < lengths[2]; stops[2]++)
                {
                    Fill(window, reels, 2, stops[2]);
                    for (stops[3] = 0; stops[3] < lengths[3]; stops[3]++)
                    {
                        Fill(window, reels, 3, stops[3]);
                        for (stops[4] = 0; stops[4] < lengths[4]; stops[4]++)
                        {
                            Fill(window, reels, 4, stops[4]);
                            acc.Add(game, window, multiplier);
                        }
                    }
                }
            }

            parts[first] = acc;
        });

        var total = new Accumulator(game.Symbols.Count);
        foreach (var part in parts)
        {
            total.Merge(game, part);
        }

        BigInteger cycle = 1;
        foreach (var length in lengths)
        {
            cycle *= length;
        }

        return new ReelStats(
            cycle,
            total.Units,
            total.Hits,
            total.Triggers,
            game.Feature.Retrigger ? total.Triggers * game.FreeSpinsAwarded : 0,
            total.BySymbol.ToDictionary(p => p.Key, p => (BigInteger)p.Value),
            total.ByWin.ToDictionary(p => p.Key, p => (BigInteger)p.Value));
    }

    private static void Fill(byte[,] window, IReadOnlyList<IReadOnlyList<byte>> reels, int reel, int stop)
    {
        var strip = reels[reel];
        for (var row = 0; row < GameDefinition.Rows; row++)
        {
            window[reel, row] = strip[(stop + row) % strip.Count];
        }
    }

    private sealed class Accumulator(int symbols)
    {
        private const int MaxCount = 15;

        public long Units;
        public long Hits;
        public long Triggers;
        public readonly Dictionary<string, long> BySymbol = [];
        public readonly Dictionary<long, long> ByWin = [];
        private readonly long[,] symbolCount = new long[symbols, MaxCount + 1];
        private readonly long[] scatterCount = new long[16];

        public void Add(GameDefinition game, byte[,] window, int multiplier)
        {
            long spin = 0;
            foreach (var rows in game.Lines)
            {
                var units = Evaluator.LineUnits(game, window, rows, out var symbol, out var count, out _);
                if (units > 0)
                {
                    spin += units;
                    symbolCount[symbol, Math.Min(count, MaxCount)] += units * multiplier;
                }
            }

            var scatters = Evaluator.Scatters(game, window);
            if (scatters >= 3)
            {
                long scatterUnits = (long)game.ScatterPays[Math.Min(scatters, 5) - 3] * game.LineCount;
                spin += scatterUnits;
                scatterCount[scatters] += scatterUnits * multiplier;
                if (game.FreeSpinsAwarded > 0)
                {
                    Triggers++;
                }
            }

            spin *= multiplier;
            Units += spin;
            if (spin > 0)
            {
                Hits++;
            }

            ByWin[spin] = ByWin.GetValueOrDefault(spin) + 1;
        }

        public void Merge(GameDefinition game, Accumulator other)
        {
            Units += other.Units;
            Hits += other.Hits;
            Triggers += other.Triggers;
            foreach (var (win, count) in other.ByWin)
            {
                ByWin[win] = ByWin.GetValueOrDefault(win) + count;
            }

            for (var s = 0; s < symbols; s++)
            {
                for (var c = 3; c <= MaxCount; c++)
                {
                    if (other.symbolCount[s, c] > 0)
                    {
                        var key = $"{game.Name((byte)s)}:{c:D2}";
                        BySymbol[key] = BySymbol.GetValueOrDefault(key) + other.symbolCount[s, c];
                    }
                }
            }

            for (var n = 3; n < 16; n++)
            {
                if (other.scatterCount[n] > 0)
                {
                    var key = $"Scatter:{n:D2}";
                    BySymbol[key] = BySymbol.GetValueOrDefault(key) + other.scatterCount[n];
                }
            }
        }
    }
}
