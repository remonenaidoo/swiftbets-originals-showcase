using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SwiftBets.Originals.Domain.Slot;

/// <summary>
/// SHA-256 of a canonical text form of a <see cref="GameDefinition"/>: any change to strips, lines or pays changes it.
/// Lines for split symbols and feature rules appear only when a game uses them, so a game that uses neither keeps
/// the hash it had before they existed.
/// </summary>
public static class DefinitionHash
{
    public static string Of(GameDefinition game) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(game))));

    public static string Canonical(GameDefinition game)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"game {game.GameId}\n");
        if (game.Symbols.Any(s => s.Weight != 1 || s.PaysAs is not null))
        {
            text.Append("symbols ").AppendJoin(',', game.Symbols.Select(s => $"{s.Name}:{s.Kind}:{s.Weight}:{s.PaysAs ?? s.Name}")).Append('\n');
        }

        Reels("base", game.BaseReels);
        Reels("free", game.FreeReels);
        foreach (var line in game.Lines)
        {
            text.Append("line ").AppendJoin(',', line).Append('\n');
        }

        foreach (var (symbol, pays) in game.LinePays.OrderBy(p => p.Key))
        {
            text.Append(CultureInfo.InvariantCulture, $"pay {game.Name(symbol)} ").AppendJoin(',', pays).Append('\n');
        }

        text.Append("scatter ").AppendJoin(',', game.ScatterPays).Append('\n');
        text.Append(CultureInfo.InvariantCulture, $"feature {game.Feature.Awarded} x{game.Feature.Multiplier} cap {game.MaxWinStakes}\n");
        if (!game.Feature.Retrigger || game.Feature.LockSplits)
        {
            text.Append(CultureInfo.InvariantCulture, $"rules retrigger={game.Feature.Retrigger} lock={game.Feature.LockSplits}\n");
        }

        return text.ToString();

        void Reels(string name, IReadOnlyList<IReadOnlyList<byte>> reels)
        {
            for (var reel = 0; reel < reels.Count; reel++)
            {
                text.Append(CultureInfo.InvariantCulture, $"{name} {reel} ").AppendJoin(',', reels[reel].Select(game.Name)).Append('\n');
            }
        }
    }
}
