using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SwiftBets.Originals.Application.Ports;

namespace SwiftBets.Originals.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddOriginalsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["ConnectionStrings:SbOriginals"] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException("Configuration 'ConnectionStrings:SbOriginals' is required.");
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IRoundStore, RoundStore>();
        services.AddHttpClient<IWallet, WalletClient>(http => http.Timeout = TimeSpan.FromSeconds(5));
        services.AddHostedService<WinRetryJob>();
        return services;
    }
}
