using Apollo.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

namespace Apollo.Web.Auth;

public static class AuthServiceCollectionExtensions
{
    /// <summary>The data protection application name; keeps the key ring stable across container names and paths.</summary>
    public const string DataProtectionApplicationName = "Apollo";

    private static readonly TimeSpan _sessionLifetime = TimeSpan.FromDays(14);
    private static readonly TimeSpan _externalLoginLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Registers the BFF cookie authentication (scope §6.1): the session cookie (HttpOnly, Secure, SameSite=Lax,
    /// sliding), the temporary external cookie platform handlers sign in to, data protection keys in
    /// <see cref="ApolloDbContext"/>, the antiforgery header for <c>/auth/logout</c> and the forwarded headers of the
    /// reverse proxy. Platform identity providers are added separately.
    /// </summary>
    public static IServiceCollection AddApolloAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = AuthSchemes.Session;
                options.DefaultSignInScheme = AuthSchemes.External;
            })
            .AddCookie(AuthSchemes.Session, options =>
            {
                options.Cookie.Name = ".Apollo.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = _sessionLifetime;
                options.SlidingExpiration = true;
                options.LoginPath = "/auth/login";
                options.ReturnUrlParameter = "returnUrl";
                options.Events.OnRedirectToLogin = context => RedirectOrStatus(context, StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = context => RedirectOrStatus(context, StatusCodes.Status403Forbidden);
            })
            .AddCookie(AuthSchemes.External, options =>
            {
                options.Cookie.Name = ".Apollo.External";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = _externalLoginLifetime;
                options.SlidingExpiration = false;
            });

        services.AddAuthorization();
        services.AddScoped<SignInCompletion>();
        services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");

        services.AddDataProtection()
            .SetApplicationName(DataProtectionApplicationName)
            .PersistKeysToDbContext<ApolloDbContext>();

        // apollo-web is reachable only through the reverse proxy (docs/deployment.md), which sets these headers. They make
        // the request scheme and host, and so the OAuth redirect URI, match the public URL.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    // API callers (the WASM client) need a status code, not a login page.
    private static Task RedirectOrStatus(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        if (IsApiRequest(context.Request))
        {
            context.Response.StatusCode = statusCode;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Requests from code rather than a browser page (<c>/api/**</c> and the client's <c>/auth/me</c>,
    /// <c>/auth/logout</c>, <c>/auth/antiforgery</c>): they get 401/403 instead of a login redirect and no HTML error
    /// page.
    /// </summary>
    internal static bool IsApiRequest(HttpRequest request) =>
        request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        || request.Path.StartsWithSegments(AuthEndpoints.MePath, StringComparison.OrdinalIgnoreCase)
        || request.Path.StartsWithSegments(AuthEndpoints.LogoutPath, StringComparison.OrdinalIgnoreCase)
        || request.Path.StartsWithSegments(AuthEndpoints.AntiforgeryPath, StringComparison.OrdinalIgnoreCase);
}
