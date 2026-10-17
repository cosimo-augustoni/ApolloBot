using System.Security.Claims;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Identity;
using Apollo.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Web.Tests.Auth;

/// <summary>
/// Test-only sign-in shortcuts, put in front of the app's pipeline:
/// <see cref="SessionSignInPath"/> issues the session cookie for an internal user without a platform login or a
/// database (its access was just checked: no admin claim), <see cref="PlatformSignInPath"/> runs the real
/// <see cref="SignInCompletion"/> for a <see cref="FakeIdentityProvider"/> user and issues a browser-session cookie
/// (no expiry date, so a test clock far from the wall clock cannot expire it in the client),
/// <see cref="ExternalSignInPath"/> plays the OAuth callback of <see cref="FakeIdentityProvider"/>, and
/// <see cref="ProtectedApiPath"/> / <see cref="ProtectedPagePath"/> challenge the default scheme as an endpoint with an
/// authorization policy would for an anonymous user.
/// </summary>
public sealed class TestSignInEndpoints : IStartupFilter
{
    public const string SessionSignInPath = "/test/sign-in";
    public const string PlatformSignInPath = "/test/platform-sign-in";
    public const string ExternalSignInPath = "/test/external-sign-in";
    public const string ProtectedApiPath = "/api/test/protected";
    public const string ProtectedPagePath = "/test/protected";

    public static readonly Guid UserId = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");
    public const string DisplayName = "Nelly";
    public const string AvatarUrl = "https://cdn.discordapp.com/avatars/42/abc.png";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Path == SessionSignInPath)
            {
                var now = context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
                var principal = ApolloPrincipal.Create(UserId, PlatformKind.Discord, DisplayName, AvatarUrl, now);
                await context.SignInAsync(AuthSchemes.Session, ApolloPrincipal.WithAccess(principal, isAdministrator: false, now));
                return;
            }

            if (context.Request.Path == PlatformSignInPath)
            {
                var identity = new PlatformIdentity(new PlatformRef(PlatformKind.Discord, context.Request.Query["id"]!), "Nelly", null);
                var completion = context.RequestServices.GetRequiredService<SignInCompletion>();
                await context.SignInAsync(AuthSchemes.Session, await completion.CompleteAsync(identity, context.RequestAborted));
                return;
            }

            if (context.Request.Path == ExternalSignInPath)
            {
                var query = context.Request.Query;
                var identity = FakeIdentityProvider.CreateIdentity(query["id"]!, query["name"]!, query["avatar"].FirstOrDefault());
                await context.SignInAsync(AuthSchemes.External, new ClaimsPrincipal(identity));
                return;
            }

            if (context.Request.Path == ProtectedApiPath || context.Request.Path == ProtectedPagePath)
            {
                await context.ChallengeAsync();
                return;
            }

            await nextMiddleware(context);
        });

        next(app);
    };
}
