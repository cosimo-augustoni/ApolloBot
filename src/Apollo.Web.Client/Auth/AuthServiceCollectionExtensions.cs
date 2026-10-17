using Microsoft.AspNetCore.Components.Authorization;

namespace Apollo.Web.Client.Auth;

public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// Registers the client auth state (<see cref="ApolloAuthenticationStateProvider"/>, cascaded to every component)
    /// and authorization for <c>AuthorizeView</c> and <c>[Authorize]</c> pages with the <see cref="ApolloPolicies"/>. Needs <see cref="Api.IAuthClient"/> (<c>AddApolloApiClients</c>).
    /// </summary>
    public static IServiceCollection AddApolloAuthentication(this IServiceCollection services)
    {
        services.AddAuthorizationCore(options => options.AddApolloPolicies());
        services.AddCascadingAuthenticationState();
        services.AddScoped<ApolloAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ApolloAuthenticationStateProvider>());

        return services;
    }
}
