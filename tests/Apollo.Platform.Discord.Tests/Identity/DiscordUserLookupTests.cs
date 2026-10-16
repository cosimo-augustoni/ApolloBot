using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Identity;
using Apollo.Platform.Discord.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Apollo.Platform.Discord.Tests.Identity;

public sealed class DiscordUserLookupTests
{
    [Fact]
    public async Task CreatingTicket_ShouldAddUserClaimsFromUsersMeAndNoToken()
    {
        var handler = new UsersMeHandler("""{ "id": "42", "username": "nelly", "global_name": "Nelly", "avatar": null }""");
        var (context, identity) = CreateContext(handler, accessToken: "secret-access-token");

        await context.Options.Events.CreatingTicket(context);

        handler.Authorization.ShouldBe("Bearer secret-access-token");
        handler.RequestUri.ShouldBe(new Uri("https://discord.com/api/users/@me"));
        new DiscordIdentityProvider().GetIdentity(new ClaimsPrincipal(identity))
            .ShouldBe(new PlatformIdentity(new PlatformRef(PlatformKind.Discord, "42"), "Nelly", null));
        identity.Claims.ShouldNotContain(c => c.Value.Contains("secret-access-token"));
        context.Properties.GetTokens().ShouldBeEmpty();
    }

    private static (OAuthCreatingTicketContext Context, ClaimsIdentity Identity) CreateContext(HttpMessageHandler handler, string accessToken)
    {
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{DiscordOAuthOptions.SectionName}:ClientId"] = "client-id",
                    [$"{DiscordOAuthOptions.SectionName}:ClientSecret"] = "client-secret",
                })
                .Build())
            .AddLogging()
            .AddDiscordIdentityProvider()
            .BuildServiceProvider();
        var options = services.GetRequiredService<IOptionsMonitor<OAuthOptions>>().Get(DiscordIdentityProvider.Scheme);
        var identity = new ClaimsIdentity(DiscordIdentityProvider.Scheme);
        var tokens = OAuthTokenResponse.Success(JsonDocument.Parse($$"""{ "access_token": "{{accessToken}}", "token_type": "Bearer" }"""));
        var scheme = new AuthenticationScheme(DiscordIdentityProvider.Scheme, null, typeof(OAuthHandler<OAuthOptions>));

        var context = new OAuthCreatingTicketContext(
            new ClaimsPrincipal(identity), new AuthenticationProperties(), new DefaultHttpContext(), scheme, options,
            new HttpClient(handler), tokens, JsonDocument.Parse("{}").RootElement);
        return (context, identity);
    }

    private sealed class UsersMeHandler(string json) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
