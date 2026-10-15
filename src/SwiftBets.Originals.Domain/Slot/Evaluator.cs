namespace SwiftBets.Originals.Domain.Slot;

/// <summary>
/// Turns stops into a window and the window into pays. Lines pay the longest run from reel 1 of one symbol,
/// Wild standing in for anything but Scatter; Wild never pays on its own, so a run that opens with Wilds takes
/// the first other symbol. Scatters pay anywhere, in total stakes, and 3 or more trigger the feature.
/// </summary>
public static class Evaluator
{
    public static Symbol[,] Window(IReadOnlyList<IReadOnlyList<Symbol>> reels, int[] stops)
    {
        var window = new Symbol[reels.Count, GameDefinition.Rows];
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

    public static SpinOutcome Evaluate(GameDefinition game, IReadOnlyList<IReadOnlyList<Symbol>> reels, int[] stops, int multiplier)
    {
        var window = Window(reels, stops);
        var lines = new List<LineWin>();
        long lineUnits = 0;
        for (var line = 0; line < game.Lines.Count; line++)
        {
            if (LineWinOf(game, window, game.Lines[line]) is { } win)
            {
                lines.Add(win with { Line = line });
                lineUnits += win.Units;
            }
        }

        var scatters = 0;
        foreach (var symbol in window)
        {
            if (symbol == Symbol.Scatter)
            {
                scatters++;
            }
        }

        long scatterUnits = scatters >= 3 ? (long)game.ScatterPays[Math.Min(scatters, 5) - 3] * game.LineCount : 0;
        return new SpinOutcome(stops, window, lines, scatters, scatterUnits, (lineUnits + scatterUnits) * multiplier, scatters >= 3);
    }

    /// <summary>The pay on one line, or null.</summary>
    public static LineWin? LineWinOf(GameDefinition game, Symbol[,] window, int[] rows)
    {
        var units = LineUnits(game, window, rows, out var symbol, out var count);
        return units == 0 ? null : new LineWin(0, symbol, count, units);
    }

    /// <summary>
    /// The pay on one line in line-bet units (0 for none), with the paying symbol and run length. Allocation-free:
    /// the full-cycle enumeration calls it hundreds of millions of times, through this same code.
    /// </summary>
    public static long LineUnits(GameDefinition game, Symbol[,] window, int[] rows, out Symbol symbol, out int count)
    {
        symbol = Symbol.Wild;
        count = 0;
        for (var reel = 0; reel < rows.Length; reel++)
        {
            var s = window[reel, rows[reel]];
            if (s == Symbol.Scatter)
            {
                break;
            }

            if (s == Symbol.Wild)
            {
                count++;
                continue;
            }

            if (symbol == Symbol.Wild)
            {
                symbol = s;
                count++;
                continue;
            }

            if (s != symbol)
            {
                break;
            }

            count++;
        }

        if (symbol == Symbol.Wild || count < 3 || !game.LinePays.TryGetValue(symbol, out var pays))
        {
            return 0;
        }

        return pays[count - 3];
    }
}
