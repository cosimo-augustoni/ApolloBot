using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Identity;
using Apollo.Platform.Discord.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Apollo.Platform.Discord.Tests.Identity;

public sealed class DiscordIdentityRegistrationTests
{
    [Fact]
    public void AddDiscordIdentityProvider_ShouldRequestIdentifyScopeOnlyWithPkceAndNoSavedTokens()
    {
        using var host = Build(ValidSettings());

        var options = host.Services.GetRequiredService<IOptionsMonitor<OAuthOptions>>().Get(DiscordIdentityProvider.Scheme);

        options.Scope.ShouldBe(["identify"]);
        options.UsePkce.ShouldBeTrue();
        options.SaveTokens.ShouldBeFalse();
        options.CallbackPath.Value.ShouldBe("/signin-discord");
        options.AuthorizationEndpoint.ShouldBe("https://discord.com/oauth2/authorize");
        options.TokenEndpoint.ShouldBe("https://discord.com/api/oauth2/token");
        options.UserInformationEndpoint.ShouldBe("https://discord.com/api/users/@me");
        options.ClientId.ShouldBe("client-id");
        options.ClientSecret.ShouldBe("client-secret");
    }

    [Fact]
    public void AddDiscordIdentityProvider_ShouldRegisterPlatformIdentityProvider()
    {
        using var host = Build(ValidSettings());

        host.Services.GetServices<IPlatformIdentityProvider>().ShouldHaveSingleItem().SchemeName.ShouldBe(DiscordIdentityProvider.Scheme);
    }

    [Fact]
    public void AddDiscordIdentityProvider_ShouldGrantAccessThroughTheGuildTheBotRecords()
    {
        using var host = Build(ValidSettings());

        var provider = host.Services.GetServices<IPlatformIdentityProvider>().ShouldHaveSingleItem();

        provider.Guild.ShouldBe(new PlatformRef(PlatformKind.Discord, "111111111111111111"));
    }

    [Fact]
    public void Start_WhenGuildIdIsMissing_ShouldFailWithValidationErrorNamingTheKey()
    {
        var settings = ValidSettings();
        settings.Remove("Apollo:Platform:Discord:GuildId");
        using var host = Build(settings);

        var exception = Should.Throw<Exception>(() => host.Services.GetRequiredService<IStartupValidator>().Validate());

        exception.ToString().ShouldContain("Apollo:Platform:Discord:GuildId is required");
    }

    [Fact]
    public async Task RemoteFailure_WhenUserCancelsAtDiscord_ShouldRedirectHomeInsteadOfFailing()
    {
        using var host = Build(ValidSettings());
        var options = host.Services.GetRequiredService<IOptionsMonitor<OAuthOptions>>().Get(DiscordIdentityProvider.Scheme);
        var http = new DefaultHttpContext();
        var context = new RemoteFailureContext(
            http, new AuthenticationScheme(DiscordIdentityProvider.Scheme, null, typeof(OAuthHandler<OAuthOptions>)), options,
            new AuthenticationFailureException("access_denied"));

        await options.Events.RemoteFailure(context);

        context.Result.ShouldNotBeNull().Handled.ShouldBeTrue();
        http.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
        http.Response.Headers.Location.ToString().ShouldBe("/");
    }

    [Theory]
    [InlineData("ClientId")]
    [InlineData("ClientSecret")]
    public void Start_WhenOAuthSettingIsMissing_ShouldFailWithValidationErrorNamingTheKey(string key)
    {
        var settings = ValidSettings();
        settings.Remove($"{DiscordOAuthOptions.SectionName}:{key}");
        using var host = Build(settings);

        var exception = Should.Throw<Exception>(() => host.Services.GetRequiredService<IStartupValidator>().Validate());

        exception.ToString().ShouldContain($"Apollo:Platform:Discord:OAuth:{key} is required");
    }

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        [$"{DiscordOAuthOptions.SectionName}:ClientId"] = "client-id",
        [$"{DiscordOAuthOptions.SectionName}:ClientSecret"] = "client-secret",
        ["Apollo:Platform:Discord:GuildId"] = "111111111111111111",
    };

    private static IHost Build(IDictionary<string, string?> settings)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddDiscordIdentityProvider();
        return builder.Build();
    }
}
