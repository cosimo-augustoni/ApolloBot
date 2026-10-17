using Apollo.Data;
using Apollo.Data.Guilds;
using Apollo.Data.Sessions;
using Apollo.Web.Client.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apollo.Web.Auth;

public static class AuthServiceCollectionExtensions
{
    /// <summary>The data protection application name; keeps the key ring stable across container names and paths.</summary>
    public const string DataProtectionApplicationName = "Apollo";

    /// <summary>
    /// The client's "Kein Zugriff" page (<c>Pages/NoAccess.razor</c>). A full page load of a page whose policy the user
    /// does not meet (e.g. <c>/admin</c> without the admin claim) is redirected here.
    /// </summary>
    public const string NoAccessPath = "/kein-zugriff";

    private static readonly TimeSpan _sessionLifetime = TimeSpan.FromDays(14);
    private static readonly TimeSpan _externalLoginLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Registers the BFF cookie authentication (scope §6.1): the session cookie (HttpOnly, Secure, SameSite=Lax,
    /// sliding, its access claims re-checked by <see cref="AccessRevalidator"/> every
    /// <see cref="WebAuthOptions.RevalidationInterval"/>), the <see cref="ApolloPolicies"/>, the temporary external
    /// cookie platform handlers sign in to, data protection keys in <see cref="ApolloDbContext"/>, the antiforgery header
    /// for <c>/auth/logout</c> and the forwarded headers of the reverse proxy. Platform identity providers are added
    /// separately.
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
                options.AccessDeniedPath = NoAccessPath;
                options.ReturnUrlParameter = "returnUrl";
                options.Events.OnRedirectToLogin = context => RedirectOrStatus(context, StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = context => RedirectOrStatus(context, StatusCodes.Status403Forbidden);
                options.Events.OnValidatePrincipal = RevalidateAccessAsync;
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

        services.AddAuthorization(options => options.AddApolloPolicies());
        services.AddOptions<WebAuthOptions>()
            .BindConfiguration(WebAuthOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<GuildAccessReader>();
        services.AddScoped<UserSignOuts>();
        services.AddScoped<AccessRevalidator>();
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

    // Runs on every request with a session cookie. A refreshed principal is written back to the cookie, so the next
    // check is one interval later; a rejected one (logged out since) ends the session and the request is anonymous.
    private static async Task RevalidateAccessAsync(CookieValidatePrincipalContext context)
    {
        var revalidator = context.HttpContext.RequestServices.GetRequiredService<AccessRevalidator>();
        var access = await revalidator.RevalidateAsync(context.Principal!, context.HttpContext.RequestAborted);
        switch (access.Outcome)
        {
            case AccessRevalidationOutcome.Refreshed:
                context.ReplacePrincipal(access.Principal!);
                context.ShouldRenew = true;
                break;
            case AccessRevalidationOutcome.Rejected:
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(AuthSchemes.Session);
                break;
        }
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
