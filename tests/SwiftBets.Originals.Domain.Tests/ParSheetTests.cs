using SwiftBets.Originals.Domain.Slot;

namespace SwiftBets.Originals.Domain.Tests;

public sealed class ParSheetTests
{
    /// <summary>Changing strips, lines or pays without regenerating the PAR sheet (maths tool: par) fails here.</summary>
    [Fact]
    public void Sheet_matches_the_definition()
    {
        var sheet = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "gilded-savanna.md"));

        var recorded = sheet.Single(l => l.StartsWith("Definition hash: ", StringComparison.Ordinal))["Definition hash: ".Length..];

        recorded.ShouldBe(DefinitionHash.Of(GildedSavanna.Definition));
    }
}
