using System.Security.Cryptography;
using System.Text.Json.Serialization;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Observability;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Originals.Api.Endpoints;
using SwiftBets.Originals.Api.Hubs;
using SwiftBets.Originals.Application;
using SwiftBets.Originals.Application.Instant;
using SwiftBets.Originals.Application.Ports;
using SwiftBets.Originals.Domain.Instant;
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
builder.Services.Configure<RtpOptions>(builder.Configuration.GetSection("Originals:Rtp"));
builder.Services.AddSingleton<IGameRates, GameRates>();
builder.Services.AddSingleton(new SlotEngines(OriginalsGames.Catalog, RandomNumberGenerator.GetInt32));
builder.Services.AddSingleton<SpinHandler>();
builder.Services.AddSingleton<FeatureHandler>();
builder.Services.AddSingleton<StateQuery>();
builder.Services.AddSingleton<MinesHandler>();
builder.Services.AddSingleton<InstantQueries>();
builder.Services.AddSingleton<TowersHandler>();
builder.Services.AddSingleton<HigherLowerHandler>();
builder.Services.AddSingleton<OneShotHandler>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<ICrashBroadcast, SignalRCrashBroadcast>();
builder.Services.AddSingleton(sp => ActivatorUtilities.CreateInstance<CrashRoom>(sp, (Func<string>)Fair.NewSeed));

var app = builder.Build();
app.UseSwiftBetsObservability();
app.UseSwiftBetsWeb();
app.MapSwiftBetsOperationalEndpoints();
app.UseStaticFiles();
app.MapSlotEndpoints();
app.MapInstantEndpoints();
app.MapHub<CrashHub>(CrashHub.Path).AllowAnonymous();
app.Run();
return 0;

public partial class Program;
