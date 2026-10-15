using System.Reflection;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Api;

public static class AppInfoEndpoints
{
    /// <summary>Maps <c>GET /api/app-info</c>: the web host's version and hosting environment.</summary>
    public static IEndpointRouteBuilder MapAppInfoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/app-info", (IHostEnvironment environment) =>
            TypedResults.Ok(new AppInfoDto(Version, environment.EnvironmentName)));

        return endpoints;
    }

    /// <summary>Informational version without the <c>+commit</c> suffix the SDK appends.</summary>
    private static string Version { get; } =
        (typeof(AppInfoEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0")
            .Split('+')[0];
}
