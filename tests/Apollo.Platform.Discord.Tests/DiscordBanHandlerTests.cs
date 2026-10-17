using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Discord.Tests.Fakes;
using Apollo.Testing;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordBanHandlerTests
{
    private const ulong ForeignGuildId = 222222222222222222;

    private readonly MappingHarness _harness = new();

    [Fact]
    public async Task GuildBanAdd_WhenUserIsBanned_ShouldDispatchMemberBannedStampedOnReceipt()
    {
        await ((IGuildBanAddGatewayHandler)Handler()).HandleAsync(NetCordPayloads.GuildBan(DiscordHost.GuildId, userId: 42));

        var banned = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMemberBanned>();
        banned.Guild.ShouldBe(Ref(DiscordHost.GuildId));
        banned.OccurredAt.ShouldBe(TestTime.Now);
        banned.User.ShouldBe(Ref(42));
    }

    [Fact]
    public async Task GuildBanRemove_WhenBanIsLifted_ShouldDispatchMemberUnbannedStampedOnReceipt()
    {
        await ((IGuildBanRemoveGatewayHandler)Handler()).HandleAsync(NetCordPayloads.GuildBan(DiscordHost.GuildId, userId: 42));

        var unbanned = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMemberUnbanned>();
        unbanned.Guild.ShouldBe(Ref(DiscordHost.GuildId));
        unbanned.OccurredAt.ShouldBe(TestTime.Now);
        unbanned.User.ShouldBe(Ref(42));
    }

    [Fact]
    public async Task GuildBanAdd_WhenBanIsInAnotherGuild_ShouldNotDispatch()
    {
        await ((IGuildBanAddGatewayHandler)Handler()).HandleAsync(NetCordPayloads.GuildBan(ForeignGuildId, userId: 42));

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    private static PlatformRef Ref(ulong snowflake) => DiscordIds.ToRef(snowflake);

    private DiscordBanHandler Handler() => new(_harness.Mapper, _harness.Time);
}
