using Apollo.Data.Identity;
using CmdScale.EntityFrameworkCore.TimescaleDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Apollo.Data;

public static class ApolloDataExtensions
{
    /// <summary>
    /// Registers <see cref="ApolloDbContext"/> through the Aspire Npgsql EF integration (connection string
    /// <c>ConnectionStrings:apollo</c>, health check, tracing, no retry strategy) plus CmdScale, binds
    /// <see cref="DataOptions"/> and registers <see cref="IPlatformIdResolver"/> (scoped, like the context) with one
    /// cache per process.
    /// </summary>
    public static TBuilder AddApolloDbContext<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // No retries: Aspire's default NpgsqlRetryingExecutionStrategy throws on user-initiated transactions, which the
        // bot uses (guild sync, tracking gate, voice recording). Wrapping each one in the strategy instead would need
        // every block, in-memory state included, to be safe to replay. Transient errors are handled where they matter:
        // the tracking gate reloads every 30 s, the voice startup close is retried, the next guild snapshot resyncs, and
        // DatabaseMigrator retries while the database is unreachable. configureSettings runs after the configuration is
        // bound, so configuration cannot turn the retries back on (docs/scope.md D17).
        builder.AddNpgsqlDbContext<ApolloDbContext>(
            ApolloDbContext.ConnectionName,
            configureSettings: settings => settings.DisableRetry = true,
            configureDbContextOptions: options => options.UseTimescaleDb());

        builder.Services.AddOptions<DataOptions>()
            .BindConfiguration(DataOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddSingleton(TimeProvider.System);

        builder.Services.AddSingleton<PlatformIdCache>();
        builder.Services.AddScoped<IPlatformIdResolver, PlatformIdResolver>();

        return builder;
    }
}
