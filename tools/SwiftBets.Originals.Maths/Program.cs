using System.Diagnostics;
using System.Globalization;
using SwiftBets.Originals.Games;
using SwiftBets.Originals.Maths;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
string? Arg(string name) => Array.IndexOf(args, name) is var at and >= 0 && at + 1 < args.Length ? args[at + 1] : null;
if (args.FirstOrDefault() == "mines")
{
    Directory.CreateDirectory("docs/par-sheet");
    return InstantSheets.Mines("docs/par-sheet/mines.md", Arg("--layouts") is { } l ? long.Parse(l, CultureInfo.InvariantCulture) : 10_000_000);
}

if (args.FirstOrDefault() == "crash")
{
    Directory.CreateDirectory("docs/par-sheet");
    return InstantSheets.Crash("docs/par-sheet/crash.md", Arg("--rounds") is { } r ? long.Parse(r, CultureInfo.InvariantCulture) : 1_000_000_000);
}

var slot = OriginalsGames.Catalog.Find(Arg("--game") ?? GildedSavanna.GameId);
if (slot is null)
{
    Console.Error.WriteLine($"unknown game; known: {string.Join(", ", OriginalsGames.Catalog.All.Select(g => g.Id))}");
    return 2;
}

var game = slot.Definition;
// Tuning only: --counts file.json  {"base":[{"Lion":2,...} x5], "free":[... x5]} tries other strips without a rebuild.
if (Arg("--counts") is { } countsFile)
{
    var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(countsFile));
    IReadOnlyDictionary<string, int>[] Read(string name) => [.. doc.RootElement.GetProperty(name).EnumerateArray()
        .Select(r => (IReadOnlyDictionary<string, int>)r.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32()))];
    game = OriginalsGames.Builders[slot.Id](Read("base"), Read("free"));
}

var features = Arg("--features") is { } f ? long.Parse(f, CultureInfo.InvariantCulture) : 100_000_000;
var command = args.FirstOrDefault() ?? "rtp";
var clock = Stopwatch.StartNew();
var sheet = $"docs/par-sheet/{slot.Id}";
switch (command)
{
    case "rtp":
        var rtp = Rtp.Compute(game, features, quiet: true);
        Console.WriteLine($"base     {rtp.BaseRtp:P4}");
        Console.WriteLine($"feature  {rtp.FeatureRtp:P4}{(rtp.Exact ? "" : $" ± {rtp.FeatureRtpHalfWidth:P4}")}   (1 in {1 / rtp.TriggerProbability:F1}, r = {rtp.RetriggerSpinsPerSpin:F5}, N = {rtp.ExpectedFreeSpins:F3}, V = {rtp.FeatureValueUnits / game.LineCount:F2}x)");
        Console.WriteLine($"total    {rtp.Total:P4}");
        Console.WriteLine($"hit freq {rtp.HitFrequency:P2}   sigma(base) {rtp.BaseSigma:F3}   [{clock.Elapsed.TotalSeconds:F1}s]");
        return 0;
    case "par":
        Directory.CreateDirectory("docs/par-sheet");
        ParSheet.Write(slot with { Definition = game }, Rtp.Compute(game, features), ParSheet.ReadSimulation(args), $"{sheet}.md", $"{sheet}.csv");
        Console.WriteLine($"wrote {sheet}.md and .csv [{clock.Elapsed.TotalSeconds:F1}s]");
        return 0;
    case "simulate":
        var rounds = Arg("--spins") is { } s ? long.Parse(s, CultureInfo.InvariantCulture) : 1_000_000_000;
        return Simulate.Run(game, Rtp.Compute(game, features, quiet: true), rounds, $"{sheet}.simulation.json");
    default:
        Console.Error.WriteLine("usage: rtp | par [--with-simulation file] | simulate --spins N  [--game id] [--features N] [--counts file]");
        return 2;
}
