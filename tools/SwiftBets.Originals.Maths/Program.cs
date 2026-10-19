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

if (args.FirstOrDefault() == "cascade")
{
    Directory.CreateDirectory("docs/par-sheet");
    var n = Arg("--rounds") is { } r ? long.Parse(r, CultureInfo.InvariantCulture) : 1_000_000_000;
    if (Arg("--try") is { } file)
    {
        // Tuning: a JSON config {"base":{..counts}, "free":{..}, "pays":{sym:[3]}, "sun":[3], "meteors":[[v,w],..]}.
        var d = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file)).RootElement;
        Dictionary<string, int> Counts(string k) => d.GetProperty(k).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32());
        var cfg = new SwiftBets.Originals.Domain.Instant.CascadeConfig(Counts("base"), Counts("free"),
            d.GetProperty("pays").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(x => x.GetInt32()).ToArray()),
            [.. d.GetProperty("sun").EnumerateArray().Select(x => x.GetInt32())],
            [.. d.GetProperty("meteors").EnumerateArray().Select(x => (x[0].GetInt32(), x[1].GetInt32()))]);
        var t = InstantSheets.Cascade(cfg, n);
        Console.WriteLine(FormattableString.Invariant($"base {t.Base:P3} free {t.Free:P3} total {t.Total:P3} ± {t.Half:P3} hit {t.Hit:P1} fs 1 in {1 / t.Trigger:F0} capped {t.Capped}"));
        return 0;
    }

    return InstantSheets.CascadeSheet("docs/par-sheet/kalahari-gems.md", n);
}

if (args.FirstOrDefault() == "hold")
{
    Directory.CreateDirectory("docs/par-sheet");
    return InstantSheets.Hold("docs/par-sheet/highveld-hold.md", Arg("--rounds") is { } r ? long.Parse(r, CultureInfo.InvariantCulture) : 100_000_000);
}

if (args.FirstOrDefault() == "ways")
{
    Directory.CreateDirectory("docs/par-sheet");
    return InstantSheets.Ways("docs/par-sheet/cape-point-ways.md", Arg("--rounds") is { } r ? long.Parse(r, CultureInfo.InvariantCulture) : 100_000_000);
}

if (args.FirstOrDefault() == "six")
{
    Directory.CreateDirectory("docs/par-sheet");
    return SixSheets.Write("docs/par-sheet");
}

if (args.FirstOrDefault() == "instant")
{
    Directory.CreateDirectory("docs/par-sheet");
    return InstantSheets.Five("docs/par-sheet");
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
