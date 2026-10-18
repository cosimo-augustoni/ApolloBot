using Apollo.Stats.Sanity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apollo.Stats;

public static class DataSanityServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IDataSanityService"/> (scoped, like the context it reads; #40). Needs
    /// <c>AddApolloDbContext()</c>.
    /// </summary>
    public static IServiceCollection AddDataSanity(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IDataSanityService, DataSanityService>();

        return services;
    }
}
