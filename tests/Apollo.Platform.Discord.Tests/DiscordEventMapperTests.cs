using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Discord.Tests.Fakes;
using Apollo.Testing.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NetCord.Gateway;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordEventMapperTests
{
    private const ulong ForeignGuildId = 222222222222222222;

    private readonly RecordingEventDispatcher _dispatcher = new();
    private readonly FakeLogger<DiscordEventMapper> _logger = new();

    [Fact]
    public async Task ForwardAsync_WhenEventIsFromConfiguredGuild_ShouldDispatchMappedEvent()
    {
        var payload = NetCordPayloads.GuildUserRemove(DiscordHost.GuildId, userId: 42);

        await CreateMapper().ForwardAsync(nameof(GatewayClient.GuildUserRemove), payload.GuildId, MapUserRemove(payload), TestContext.Current.CancellationToken);

        var dispatched = _dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMemberLeft>();
        dispatched.Guild.ShouldBe(new PlatformRef(PlatformKind.Discord, "111111111111111111"));
        dispatched.User.ShouldBe(new PlatformRef(PlatformKind.Discord, "42"));
    }

    [Fact]
    public async Task ForwardAsync_WhenEventIsFromAnotherGuild_ShouldDropItWithoutDispatching()
    {
        var payload = NetCordPayloads.GuildUserRemove(ForeignGuildId, userId: 42);

        await CreateMapper().ForwardAsync(nameof(GatewayClient.GuildUserRemove), payload.GuildId, MapUserRemove(payload), TestContext.Current.CancellationToken);

        _dispatcher.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task ForwardAsync_WhenEventIsFromAnotherGuild_ShouldNotMapIt()
    {
        var mapped = false;

        await CreateMapper().ForwardAsync("GuildUserRemove", ForeignGuildId, guild =>
        {
            mapped = true;
            return new PlatformMemberLeftBuilder().InGuild(guild).Build();
        }, TestContext.Current.CancellationToken);

        mapped.ShouldBeFalse();
    }

    [Fact]
    public async Task ForwardAsync_WhenEventIsFromAnotherGuild_ShouldLogDebugLineWithoutPayload()
    {
        var payload = NetCordPayloads.GuildUserRemove(ForeignGuildId, userId: 42);

        await CreateMapper().ForwardAsync(nameof(GatewayClient.GuildUserRemove), payload.GuildId, MapUserRemove(payload), TestContext.Current.CancellationToken);

        var record = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Debug);
        record.StructuredState.ShouldNotBeNull().Select(p => p.Key)
            .ShouldBe(["GatewayEvent", "GuildId", "{OriginalFormat}"], ignoreOrder: true);
        record.Message.ShouldNotContain("42");
    }

    [Fact]
    public async Task ForwardAsync_WhenEventIsADirectMessage_ShouldDropItWithoutDispatching()
    {
        var message = NetCordPayloads.DirectMessage(authorId: 42);

        await CreateMapper().ForwardAsync(nameof(GatewayClient.MessageCreate), message.GuildId, guild =>
            new PlatformMemberLeftBuilder().InGuild(guild).Build(), TestContext.Current.CancellationToken);

        _dispatcher.Events.ShouldBeEmpty();
        _logger.Collector.GetSnapshot().ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Debug);
    }

    private static Func<PlatformRef, PlatformEvent> MapUserRemove(GuildUserRemoveEventArgs payload) => guild =>
        new PlatformMemberLeftBuilder().InGuild(guild).ForUser(new PlatformRef(PlatformKind.Discord, payload.User.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))).Build();

    private DiscordEventMapper CreateMapper() =>
        new(Microsoft.Extensions.Options.Options.Create(new DiscordPlatformOptions { Token = DiscordHost.Token, GuildId = DiscordHost.GuildId }), _dispatcher, _logger);
}
