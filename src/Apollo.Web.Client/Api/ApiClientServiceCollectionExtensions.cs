using Apollo.Web.Client.Api.Mock;

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

        if (section.Get<ApiClientOptions>()?.UseMockData == true)
        {
            services.AddScoped<IAppInfoClient, MockAppInfoClient>();
            services.AddScoped<IAuthClient, MockAuthClient>();
        }
        else
        {
            services.AddScoped<IAppInfoClient, HttpAppInfoClient>();
            services.AddScoped<IAuthClient, HttpAuthClient>();
        }

        return services;
    }
}
