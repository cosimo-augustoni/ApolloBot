using Apollo.Stats.Activity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apollo.Stats;

public static class ActivityStatsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IActivityStatsService"/> (scoped, like the context it reads; #35). Needs
    /// <c>AddApolloDbContext()</c>.
    /// </summary>
    public static IServiceCollection AddActivityStats(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IActivityStatsService, ActivityStatsService>();

        return services;
    }
}
