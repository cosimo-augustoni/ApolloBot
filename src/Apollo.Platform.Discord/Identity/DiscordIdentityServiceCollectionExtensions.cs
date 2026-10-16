using System.Net.Http.Headers;
using System.Text.Json;
using Apollo.Platform.Abstractions.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Apollo.Platform.Discord.Identity;

public static class DiscordIdentityServiceCollectionExtensions
{
    // https://discord.com/developers/docs/topics/oauth2#shared-resources-oauth2-urls
    private const string AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
    private const string TokenEndpoint = "https://discord.com/api/oauth2/token";
    private const string UserInformationEndpoint = "https://discord.com/api/users/@me";

    /// <summary>
    /// Registers the Discord login: an OAuth2 authorization-code handler (scheme <c>Discord</c>, callback
    /// <c>/signin-discord</c>, scope <c>identify</c> only, PKCE, tokens discarded after the user lookup) that signs in to
    /// the host's default sign-in scheme, and <see cref="IPlatformIdentityProvider"/> for it. Settings come from
    /// <see cref="DiscordOAuthOptions.SectionName"/> and are validated at startup.
    /// </summary>
    public static IServiceCollection AddDiscordIdentityProvider(this IServiceCollection services)
    {
        services.AddOptions<DiscordOAuthOptions>()
            .BindConfiguration(DiscordOAuthOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication().AddOAuth(DiscordIdentityProvider.Scheme, options =>
        {
            options.AuthorizationEndpoint = AuthorizationEndpoint;
            options.TokenEndpoint = TokenEndpoint;
            options.UserInformationEndpoint = UserInformationEndpoint;
            options.CallbackPath = "/signin-discord";
            options.Scope.Clear();
            options.Scope.Add("identify");
            options.UsePkce = true;
            options.SaveTokens = false;
            options.Events.OnCreatingTicket = AddUserClaimsAsync;
            options.Events.OnRemoteFailure = GoHome;
        });

        services.AddOptions<OAuthOptions>(DiscordIdentityProvider.Scheme)
            .Configure<IOptions<DiscordOAuthOptions>>((options, discord) =>
            {
                options.ClientId = discord.Value.ClientId!;
                options.ClientSecret = discord.Value.ClientSecret!;
            });

        services.AddSingleton<IPlatformIdentityProvider, DiscordIdentityProvider>();

        return services;
    }

    // The user cancelled at Discord (or the callback was invalid): back to the start page, still anonymous, not an error page.
    private static Task GoHome(RemoteFailureContext context)
    {
        context.Response.Redirect("/");
        context.HandleResponse();
        return Task.CompletedTask;
    }

    // The access token is used once, here, and then dropped (SaveTokens = false): the ticket holds only the user claims.
    private static async Task AddUserClaimsAsync(OAuthCreatingTicketContext context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
        response.EnsureSuccessStatusCode();

        using var user = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(context.HttpContext.RequestAborted),
            cancellationToken: context.HttpContext.RequestAborted);

        context.Identity!.AddClaims(DiscordIdentityProvider.ToClaims(DiscordIdentityProvider.MapUser(user.RootElement)));
    }
}
