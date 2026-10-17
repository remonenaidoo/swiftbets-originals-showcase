namespace SwiftBets.Originals.Domain.Slot;

/// <summary>
/// Turns stops into a window and the window into pays. A line pays when one symbol runs over at least three reels
/// from reel 1, Wilds standing in for anything but Scatter; the pay is for the run's total count, where split
/// symbols and split Wilds count 2 or 3. Wilds never pay on their own, so a run that opens with Wilds takes the
/// first other symbol. Scatters pay anywhere, in total stakes, and 3 or more trigger the feature.
/// </summary>
public static class Evaluator
{
    public static byte[,] Window(IReadOnlyList<IReadOnlyList<byte>> reels, int[] stops)
    {
        var window = new byte[reels.Count, GameDefinition.Rows];
        for (var reel = 0; reel < reels.Count; reel++)
        {
            var strip = reels[reel];
            for (var row = 0; row < GameDefinition.Rows; row++)
            {
                window[reel, row] = strip[(stops[reel] + row) % strip.Count];
            }
        }

        return window;
    }

    /// <summary>
    /// Evaluates one spin. <paramref name="locks"/> (free spins of a locking game) overwrite their cells first; the
    /// outcome's locks are then those plus every split symbol showing, when <paramref name="locking"/> is set.
    /// </summary>
    public static SpinOutcome Evaluate(GameDefinition game, IReadOnlyList<IReadOnlyList<byte>> reels, int[] stops, int multiplier, IReadOnlyList<LockedCell>? locks = null, bool locking = false)
    {
        var window = Window(reels, stops);
        foreach (var l in locks ?? [])
        {
            window[l.Reel, l.Row] = l.Symbol;
        }

        var lines = new List<LineWin>();
        long lineUnits = 0;
        for (var line = 0; line < game.Lines.Count; line++)
        {
            var units = LineUnits(game, window, game.Lines[line], out var symbol, out var count, out var run);
            if (units > 0)
            {
                lines.Add(new LineWin(line, symbol, count, run, units));
                lineUnits += units;
            }
        }

        var scatters = Scatters(game, window);
        long scatterUnits = scatters >= 3 ? (long)game.ScatterPays[Math.Min(scatters, 5) - 3] * game.LineCount : 0;
        var triggers = scatters >= 3 && game.Feature.Awarded > 0;
        return new SpinOutcome(stops, window, lines, scatters, scatterUnits, (lineUnits + scatterUnits) * multiplier, triggers, locking ? LocksAfter(game, window) : []);
    }

    public static int Scatters(GameDefinition game, byte[,] window)
    {
        var scatters = 0;
        foreach (var symbol in window)
        {
            if (game.Kinds[symbol] == SymbolKind.Scatter)
            {
                scatters++;
            }
        }

        return scatters;
    }

    /// <summary>The pay on one line, or null.</summary>
    public static LineWin? LineWinOf(GameDefinition game, byte[,] window, int[] rows)
    {
        var units = LineUnits(game, window, rows, out var symbol, out var count, out var run);
        return units == 0 ? null : new LineWin(0, symbol, count, run, units);
    }

    /// <summary>
    /// The pay on one line in line-bet units (0 for none), with the paying symbol, its total count and the number of
    /// reels in the run. Allocation-free: the full-cycle enumeration calls it hundreds of millions of times, through
    /// this same code.
    /// </summary>
    public static long LineUnits(GameDefinition game, byte[,] window, int[] rows, out byte symbol, out int count, out int run)
    {
        symbol = GameDefinition.None;
        count = 0;
        run = 0;
        var kinds = game.Kinds;
        var weights = game.Weights;
        for (var reel = 0; reel < rows.Length; reel++)
        {
            var s = window[reel, rows[reel]];
            var kind = kinds[s];
            if (kind == SymbolKind.Scatter)
            {
                break;
            }

            if (kind != SymbolKind.Wild)
            {
                var pays = game.PaysAsIds[s];
                if (symbol == GameDefinition.None)
                {
                    symbol = pays;
                }
                else if (pays != symbol)
                {
                    break;
                }
            }

            count += weights[s];
            run++;
        }

        if (symbol == GameDefinition.None || run < 3 || game.PayTable[symbol] is not { } table)
        {
            return 0;
        }

        return table[Math.Min(count, table.Length + 2) - 3];
    }

    private static List<LockedCell> LocksAfter(GameDefinition game, byte[,] window)
    {
        var locked = new List<LockedCell>();
        for (var reel = 0; reel < window.GetLength(0); reel++)
        {
            for (var row = 0; row < GameDefinition.Rows; row++)
            {
                if (game.Weights[window[reel, row]] > 1)
                {
                    locked.Add(new LockedCell(reel, row, window[reel, row]));
                }
            }
        }

        return locked;
    }
}
