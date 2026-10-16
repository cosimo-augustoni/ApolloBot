using Apollo.Platform.Abstractions;
using Apollo.Platform.Discord.Tests.Fakes;
using Microsoft.Extensions.Options;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordChatPlatformRoleTests : IDisposable
{
    private static readonly PlatformRef _guild = new(PlatformKind.Discord, "111111111111111111");
    private static readonly PlatformRef _user = new(PlatformKind.Discord, "42");
    private static readonly PlatformRef _role = new(PlatformKind.Discord, "7");

    private readonly TestGateway _gateway = new();
    private readonly DiscordChatPlatform _platform;

    public DiscordChatPlatformRoleTests()
    {
        _platform = new DiscordChatPlatform(_gateway.Client, new DiscordMemberDownloader(new FakeGatewayCommands(), TimeProvider.System), Options.Create(new DiscordPlatformOptions { Token = DiscordHost.Token, GuildId = DiscordHost.GuildId }));
    }

    [Fact]
    public async Task AddRoleAsync_ForConfiguredGuild_ShouldPutTheMemberRoleOverRest()
    {
        await _platform.AddRoleAsync(_guild, _user, _role, TestContext.Current.CancellationToken);

        var request = _gateway.Rest.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Put);
        request.Path.ShouldEndWith("/guilds/111111111111111111/members/42/roles/7");
    }

    [Fact]
    public async Task RemoveRoleAsync_ForConfiguredGuild_ShouldDeleteTheMemberRoleOverRest()
    {
        await _platform.RemoveRoleAsync(_guild, _user, _role, TestContext.Current.CancellationToken);

        var request = _gateway.Rest.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Delete);
        request.Path.ShouldEndWith("/guilds/111111111111111111/members/42/roles/7");
    }

    [Fact]
    public async Task AddRoleAsync_WhenGuildIsNotTheConfiguredGuild_ShouldThrowWithoutCallingRest()
    {
        var otherGuild = new PlatformRef(PlatformKind.Discord, "222222222222222222");

        await Should.ThrowAsync<ArgumentException>(() => _platform.AddRoleAsync(otherGuild, _user, _role, TestContext.Current.CancellationToken));

        _gateway.Rest.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenRefIsFromAnotherPlatform_ShouldThrowWithoutCallingRest()
    {
        var fluxerUser = new PlatformRef(PlatformKind.Fluxer, "42");

        await Should.ThrowAsync<ArgumentException>(() => _platform.RemoveRoleAsync(_guild, fluxerUser, _role, TestContext.Current.CancellationToken));

        _gateway.Rest.Requests.ShouldBeEmpty();
    }

    public void Dispose() => _gateway.Dispose();
}
