using System.Text.Json.Serialization;
using System.Security.Cryptography;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Observability;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Originals.Api.Endpoints;
using SwiftBets.Originals.Application;
using SwiftBets.Originals.Games;
using SwiftBets.Originals.Infrastructure;

if (HealthProbe.TryRun(args) is { } probeExitCode)
{
    return probeExitCode;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddSwiftBetsObservability("swiftbets-originals");
builder.Services.AddSwiftBetsWeb();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddValidatedOptions<OriginalsOptions>(builder.Configuration, OriginalsOptions.SectionName);
builder.Services.AddOriginalsInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new SlotEngines(OriginalsGames.Catalog, RandomNumberGenerator.GetInt32));
builder.Services.AddSingleton<SpinHandler>();
builder.Services.AddSingleton<FeatureHandler>();
builder.Services.AddSingleton<StateQuery>();
builder.Services.AddSingleton<SwiftBets.Originals.Application.Instant.MinesHandler>();
builder.Services.AddSingleton<SwiftBets.Originals.Application.Instant.InstantQueries>();
builder.Services.AddSingleton<SwiftBets.Originals.Application.Instant.TowersHandler>();
builder.Services.AddSingleton<SwiftBets.Originals.Application.Instant.HigherLowerHandler>();
builder.Services.AddSingleton<SwiftBets.Originals.Application.Instant.OneShotHandler>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<SwiftBets.Originals.Application.Ports.ICrashBroadcast, SwiftBets.Originals.Api.Hubs.SignalRCrashBroadcast>();
builder.Services.AddSingleton(sp => new SwiftBets.Originals.Application.Instant.CrashRoom(
    sp.GetRequiredService<SwiftBets.Originals.Application.Ports.IRoundStore>(), sp.GetRequiredService<SwiftBets.Originals.Application.Ports.ICrashStore>(),
    sp.GetRequiredService<SwiftBets.Originals.Application.Ports.IWallet>(), sp.GetRequiredService<SwiftBets.Originals.Application.Ports.ICrashBroadcast>(),
    TimeProvider.System, SwiftBets.Originals.Domain.Instant.Fair.NewSeed));

var app = builder.Build();
app.UseSwiftBetsObservability();
app.UseSwiftBetsWeb();
app.MapSwiftBetsOperationalEndpoints();
app.UseStaticFiles();
app.MapSlotEndpoints();
app.MapInstantEndpoints();
app.MapHub<SwiftBets.Originals.Api.Hubs.CrashHub>(SwiftBets.Originals.Api.Hubs.CrashHub.Path).AllowAnonymous();
app.Run();
return 0;

public partial class Program;
