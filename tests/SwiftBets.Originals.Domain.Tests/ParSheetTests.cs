using SwiftBets.Originals.Domain.Slot;
using SwiftBets.Originals.Games;

namespace SwiftBets.Originals.Domain.Tests;

public sealed class ParSheetTests
{
    /// <summary>Changing strips, lines or pays without regenerating the PAR sheet (maths tool: par) fails here.</summary>
    [Theory]
    [InlineData(GildedSavanna.GameId)]
    [InlineData(GoldReefRush.GameId)]
    [InlineData(KarooStargazer.GameId)]
    public void Sheet_matches_the_definition(string gameId)
    {
        var sheet = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, $"{gameId}.md"));

        var recorded = sheet.Single(l => l.StartsWith("Definition hash: ", StringComparison.Ordinal))["Definition hash: ".Length..];

        recorded.ShouldBe(DefinitionHash.Of(OriginalsGames.Catalog.Find(gameId)!.Definition));
    }

    [Fact]
    public void Karoo_Stargazer_plays_Gilded_Savannas_maths_under_its_own_names()
    {
        var (karoo, gilded) = (KarooStargazer.Definition, GildedSavanna.Definition);

        karoo.BaseReels.ShouldBe(gilded.BaseReels);
        karoo.FreeReels.ShouldBe(gilded.FreeReels);
        karoo.LinePays.ShouldBe(gilded.LinePays);
        karoo.Symbols.Select(s => s.Name).ShouldContain("Leopard");
        karoo.Symbols.Select(s => s.Name).ShouldNotContain("Lion");
    }
}
