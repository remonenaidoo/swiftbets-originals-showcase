using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SwiftBets.Originals.Domain.Slot;

/// <summary>SHA-256 of a canonical text form of a <see cref="GameDefinition"/>: any change to strips, lines or pays changes it.</summary>
public static class DefinitionHash
{
    public static string Of(GameDefinition game) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(game))));

    public static string Canonical(GameDefinition game)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"game {game.GameId}\n");
        Reels("base", game.BaseReels);
        Reels("free", game.FreeReels);
        foreach (var line in game.Lines)
        {
            text.Append("line ").AppendJoin(',', line).Append('\n');
        }

        foreach (var (symbol, pays) in game.LinePays.OrderBy(p => p.Key))
        {
            text.Append(CultureInfo.InvariantCulture, $"pay {symbol} ").AppendJoin(',', pays).Append('\n');
        }

        text.Append("scatter ").AppendJoin(',', game.ScatterPays).Append('\n');
        text.Append(CultureInfo.InvariantCulture, $"feature {game.FreeSpinsAwarded} x{game.FreeSpinMultiplier} cap {game.MaxWinStakes}\n");
        return text.ToString();

        void Reels(string name, IReadOnlyList<IReadOnlyList<Symbol>> reels)
        {
            for (var reel = 0; reel < reels.Count; reel++)
            {
                text.Append(CultureInfo.InvariantCulture, $"{name} {reel} ").AppendJoin(',', reels[reel]).Append('\n');
            }
        }
    }
}
