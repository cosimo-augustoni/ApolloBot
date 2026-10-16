using Apollo.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordGatewaySetupTests
{
    [Fact]
    public void AddDiscordPlatform_WhenOptionsAreValid_ShouldConfigureExactlyTheRequiredIntents()
    {
        using var host = DiscordHost.Build(DiscordHost.ValidSettings());

        var gateway = host.Services.GetRequiredService<IOptions<GatewayClientOptions>>().Value;

        gateway.Intents.ShouldBe(
            GatewayIntents.Guilds
            | GatewayIntents.GuildMessages
            | GatewayIntents.MessageContent
            | GatewayIntents.GuildMessageReactions
            | GatewayIntents.GuildVoiceStates
            | GatewayIntents.GuildPresences
            | GatewayIntents.GuildUsers
            | GatewayIntents.GuildModeration);
    }

    [Fact]
    public void AddDiscordPlatform_WhenOptionsAreValid_ShouldPassTheTokenToTheGatewayClient()
    {
        using var host = DiscordHost.Build(DiscordHost.ValidSettings());

        var gateway = host.Services.GetRequiredService<IOptions<GatewayClientOptions>>().Value;

        gateway.Token.ShouldBe(DiscordHost.Token);
    }

    [Fact]
    public void AddDiscordPlatform_WhenOptionsAreValid_ShouldRegisterTheGatewayClient()
    {
        using var host = DiscordHost.Build(DiscordHost.ValidSettings());

        host.Services.GetService<GatewayClient>().ShouldNotBeNull();
    }

    [Fact]
    public void AddDiscordPlatform_WhenConnectIsNotSet_ShouldStartTheGatewayWithTheHost()
    {
        using var host = DiscordHost.Build(DiscordHost.ValidSettings());

        var gateway = host.Services.GetRequiredService<IOptions<GatewayClientOptions>>().Value;

        gateway.AutoStartStop.ShouldBe(true);
    }

    [Fact]
    public void AddDiscordPlatform_WhenConnectIsFalse_ShouldNotStartTheGatewayWithTheHost()
    {
        var settings = DiscordHost.ValidSettings();
        settings[$"{DiscordPlatformOptions.SectionName}:Connect"] = "false";
        using var host = DiscordHost.Build(settings);

        var gateway = host.Services.GetRequiredService<IOptions<GatewayClientOptions>>().Value;

        gateway.AutoStartStop.ShouldBe(false);
    }

    [Fact]
    public async Task Start_WhenConnectIsFalse_ShouldRunTheHostWithoutConnectingTheGateway()
    {
        var settings = DiscordHost.ValidSettings();
        settings[$"{DiscordPlatformOptions.SectionName}:Connect"] = "false";
        using var host = DiscordHost.Build(settings);

        await host.StartAsync(TestContext.Current.CancellationToken);

        host.Services.GetRequiredService<GatewayClient>().Status.ShouldBe(WebSocketStatus.Disconnected);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void AddDiscordPlatform_WhenOptionsAreValid_ShouldRegisterTheDiscordChatPlatform()
    {
        using var host = DiscordHost.Build(DiscordHost.ValidSettings());

        host.Services.GetRequiredService<IChatPlatform>().Platform.ShouldBe(PlatformKind.Discord);
    }
}
