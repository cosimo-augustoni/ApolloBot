using Apollo.Stats.Tracking;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Stats;

public static class TrackingStatusServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITrackingStatusService"/> (scoped, like the context it reads; #38). Needs
    /// <c>AddApolloDbContext()</c>.
    /// </summary>
    public static IServiceCollection AddTrackingStatus(this IServiceCollection services)
    {
        services.AddScoped<ITrackingStatusService, TrackingStatusService>();

        return services;
    }
}
