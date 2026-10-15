using System.Diagnostics;
using System.Globalization;
using SwiftBets.Originals.Domain.Slot;
using SwiftBets.Originals.Maths;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
var game = GildedSavanna.Definition;
// Tuning only: --counts file.json  {"base":[{"Lion":2,...} x5], "free":[... x5]} tries other strips without a rebuild.
if (Array.IndexOf(args, "--counts") is var at and >= 0)
{
    var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(args[at + 1]));
    IReadOnlyDictionary<Symbol, int>[] Read(string name) => [.. doc.RootElement.GetProperty(name).EnumerateArray()
        .Select(r => (IReadOnlyDictionary<Symbol, int>)r.EnumerateObject().Where(p => p.Value.GetInt32() > 0).ToDictionary(p => Enum.Parse<Symbol>(p.Name), p => p.Value.GetInt32()))];
    game = GildedSavanna.Build(Read("base"), Read("free"));
}
var command = args.FirstOrDefault() ?? "rtp";
var clock = Stopwatch.StartNew();
switch (command)
{
    case "rtp":
        var rtp = Rtp.Compute(game);
        Console.WriteLine($"base     {rtp.BaseRtp:P4}");
        Console.WriteLine($"feature  {rtp.FeatureRtp:P4}   (1 in {1 / rtp.TriggerProbability:F1}, r = {rtp.RetriggerSpinsPerSpin:F5}, N = {rtp.ExpectedFreeSpins:F3}, V = {rtp.FeatureValueUnits / game.LineCount:F2}x)");
        Console.WriteLine($"total    {rtp.Total:P4}");
        Console.WriteLine($"hit freq {rtp.HitFrequency:P2}   sigma(base) {rtp.BaseSigma:F3}   [{clock.Elapsed.TotalSeconds:F1}s]");
        return 0;
    case "par":
        Directory.CreateDirectory("docs/par-sheet");
        ParSheet.Write(game, Rtp.Compute(game), ParSheet.ReadSimulation(args), "docs/par-sheet/gilded-savanna.md", "docs/par-sheet/gilded-savanna.csv");
        Console.WriteLine($"wrote docs/par-sheet/gilded-savanna.md and .csv [{clock.Elapsed.TotalSeconds:F1}s]");
        return 0;
    case "simulate":
        var spins = Array.IndexOf(args, "--spins") is var i and >= 0 ? long.Parse(args[i + 1], CultureInfo.InvariantCulture) : 1_000_000_000;
        return Simulate.Run(game, spins, "docs/par-sheet/simulation.json");
    default:
        Console.Error.WriteLine("usage: rtp | par [--with-simulation file] | simulate --spins N");
        return 2;
}
