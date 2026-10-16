using Apollo.Platform.Abstractions.Identity;
using Apollo.Web.Client.Contracts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apollo.Web.Auth;

public static class AuthEndpoints
{
    public const string MePath = "/auth/me";

    /// <summary>
    /// Maps the BFF login endpoints (scope §6.1):
    /// <c>GET /auth/login</c> (challenge), <c>GET /auth/complete</c> (after the platform callback),
    /// <c>POST /auth/logout</c> (antiforgery-protected), <c>GET /auth/me</c> and <c>GET /auth/antiforgery</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/auth");

        auth.MapGet("/login", Login);
        auth.MapGet("/complete", CompleteAsync);
        auth.MapPost("/logout", LogoutAsync);
        auth.MapGet("/me", (HttpContext http) => TypedResults.Ok(ApolloPrincipal.ToCurrentUser(http.User)))
            .RequireAuthorization();
        auth.MapGet("/antiforgery", (HttpContext http, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(http);
            return TypedResults.Ok(new AntiforgeryTokenDto(tokens.HeaderName!, tokens.RequestToken!));
        });

        return endpoints;
    }

    /// <summary>
    /// Starts the platform login. <paramref name="platform"/> picks the provider (default: the first registered);
    /// a non-local <paramref name="returnUrl"/> is replaced by <c>/</c>.
    /// </summary>
    private static Results<ChallengeHttpResult, NotFound> Login(
        string? returnUrl, string? platform, IEnumerable<IPlatformIdentityProvider> providers)
    {
        var provider = platform is null
            ? providers.FirstOrDefault()
            : providers.FirstOrDefault(p => string.Equals(p.Platform.ToString(), platform, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            return TypedResults.NotFound();
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = $"/auth/complete?returnUrl={Uri.EscapeDataString(ReturnUrl.LocalOrRoot(returnUrl))}",
        };
        return TypedResults.Challenge(properties, [provider.SchemeName]);
    }

    /// <summary>
    /// Reads the external cookie the platform handler wrote after its callback, deletes it, maps the user and issues
    /// the session cookie. Without a valid external login it just goes home.
    /// </summary>
    private static async Task<RedirectHttpResult> CompleteAsync(
        string? returnUrl,
        HttpContext http,
        IEnumerable<IPlatformIdentityProvider> providers,
        SignInCompletion completion,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(AuthEndpoints).FullName!);
        var external = await http.AuthenticateAsync(AuthSchemes.External);
        await http.SignOutAsync(AuthSchemes.External);

        var identity = external.Succeeded
            ? providers.Select(p => p.GetIdentity(external.Principal)).FirstOrDefault(i => i is not null)
            : null;
        if (identity is null)
        {
            logger.LogWarning("Login completion without a valid platform login");
            return TypedResults.LocalRedirect("/");
        }

        var principal = await completion.CompleteAsync(identity, cancellationToken);
        await http.SignInAsync(AuthSchemes.Session, principal);
        logger.LogInformation("User {UserId} logged in with {Platform}", ApolloPrincipal.ToCurrentUser(principal).UserId, identity.User.Platform);

        return TypedResults.LocalRedirect(ReturnUrl.LocalOrRoot(returnUrl));
    }

    private static async Task<Results<NoContent, BadRequest>> LogoutAsync(HttpContext http, IAntiforgery antiforgery)
    {
        if (!await antiforgery.IsRequestValidAsync(http))
        {
            return TypedResults.BadRequest();
        }

        await http.SignOutAsync(AuthSchemes.Session);
        return TypedResults.NoContent();
    }
}
