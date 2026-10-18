using Apollo.Stats.Raw;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Stats;

public static class RawStatsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IRawStatsService"/> (scoped, like the context it reads; #36). Needs
    /// <c>AddApolloDbContext()</c>.
    /// </summary>
    public static IServiceCollection AddRawStats(this IServiceCollection services)
    {
        services.AddScoped<IRawStatsService, RawStatsService>();

        return services;
    }
}
