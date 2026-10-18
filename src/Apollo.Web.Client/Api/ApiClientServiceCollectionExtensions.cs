using Apollo.Web.Client.Api.Mock;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apollo.Web.Client.Api;

public static class ApiClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers every typed API client: the mock implementation when <see cref="ApiClientOptions.UseMockData"/> is set,
    /// otherwise the HTTP implementation, which needs a scoped <see cref="HttpClient"/> pointing at the web host.
    /// </summary>
    public static IServiceCollection AddApolloApiClients(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ApiClientOptions.SectionName);
        services.AddOptions<ApiClientOptions>().Bind(section);
        services.TryAddSingleton(TimeProvider.System);

        if (section.Get<ApiClientOptions>()?.UseMockData == true)
        {
            services.AddScoped<IAppInfoClient, MockAppInfoClient>();
            services.AddScoped<IAuthClient, MockAuthClient>();
            services.AddScoped<IRoleSettingsClient, MockRoleSettingsClient>();
            services.AddScoped<IBotStatusClient, MockBotStatusClient>();
            services.AddScoped<IDataOverviewClient, MockDataOverviewClient>();
            services.AddScoped<IServerStatsClient, MockServerStatsClient>();
        }
        else
        {
            services.AddScoped<IAppInfoClient, HttpAppInfoClient>();
            services.AddScoped<IAuthClient, HttpAuthClient>();
            services.AddScoped<IRoleSettingsClient, HttpRoleSettingsClient>();
            services.AddScoped<IBotStatusClient, HttpBotStatusClient>();
            services.AddScoped<IDataOverviewClient, HttpDataOverviewClient>();
            services.AddScoped<IServerStatsClient, HttpServerStatsClient>();
        }

        return services;
    }
}
