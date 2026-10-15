using CmdScale.EntityFrameworkCore.TimescaleDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Apollo.Data;

public static class ApolloDataExtensions
{
    /// <summary>
    /// Registers <see cref="ApolloDbContext"/> through the Aspire Npgsql EF integration (connection string
    /// <c>ConnectionStrings:apollo</c>, retries, health check, tracing) plus CmdScale, and binds <see cref="DataOptions"/>.
    /// </summary>
    public static TBuilder AddApolloDbContext<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddNpgsqlDbContext<ApolloDbContext>(
            ApolloDbContext.ConnectionName,
            configureDbContextOptions: options => options.UseTimescaleDb());

        builder.Services.AddOptions<DataOptions>()
            .BindConfiguration(DataOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddSingleton(TimeProvider.System);

        return builder;
    }
}
