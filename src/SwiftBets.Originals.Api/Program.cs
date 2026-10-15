using System.Text.Json.Serialization;
using System.Security.Cryptography;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Observability;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Originals.Api.Endpoints;
using SwiftBets.Originals.Application;
using SwiftBets.Originals.Domain.Slot;
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
builder.Services.AddSingleton(_ => new RoundEngine(GildedSavanna.Definition, RandomNumberGenerator.GetInt32));
builder.Services.AddSingleton<SpinHandler>();
builder.Services.AddSingleton<FeatureHandler>();
builder.Services.AddSingleton<StateQuery>();

var app = builder.Build();
app.UseSwiftBetsObservability();
app.UseSwiftBetsWeb();
app.MapSwiftBetsOperationalEndpoints();
app.UseStaticFiles();
app.MapSlotEndpoints();
app.Run();
return 0;

public partial class Program;
