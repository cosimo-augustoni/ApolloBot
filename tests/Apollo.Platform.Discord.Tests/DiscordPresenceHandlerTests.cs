using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Platform.Discord.Tests.Fakes;
using Apollo.Testing;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.JsonModels;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordPresenceHandlerTests
{
    private const ulong ForeignGuildId = 222222222222222222;
    private const ulong AliceId = 42;

    private readonly MappingHarness _harness = new();

    [Fact]
    public async Task PresenceUpdate_WhenUserHasActivities_ShouldDispatchStatusAndActivitiesStampedOnReceipt()
    {
        var startedAt = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);

        await HandleAsync(NetCordPayloads.Presence(DiscordHost.GuildId, AliceId, UserStatusType.Idle,
            new JsonUserActivity { Type = UserActivityType.Playing, Name = "Factorio", Timestamps = new() { StartTime = startedAt } },
            new JsonUserActivity { Type = UserActivityType.Listening, Name = "Spotify" }));

        var updated = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformPresenceUpdated>();
        updated.Guild.ShouldBe(Ref(DiscordHost.GuildId));
        updated.OccurredAt.ShouldBe(TestTime.Now);
        updated.Presence.User.ShouldBe(Ref(AliceId));
        updated.Presence.Status.ShouldBe(PlatformPresenceStatus.Idle);
        updated.Presence.Activities.ShouldBe(
        [
            new PlatformActivity(PlatformActivityType.Playing, "Factorio", startedAt),
            new PlatformActivity(PlatformActivityType.Listening, "Spotify", null),
        ]);
    }

    [Fact]
    public async Task PresenceUpdate_WhenUserWentOffline_ShouldDispatchAnOfflinePresenceWithoutActivities()
    {
        await HandleAsync(NetCordPayloads.Presence(DiscordHost.GuildId, AliceId, UserStatusType.Offline));

        var presence = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformPresenceUpdated>().Presence;
        presence.Status.ShouldBe(PlatformPresenceStatus.Offline);
        presence.Activities.ShouldBeEmpty();
    }

    [Fact]
    public async Task PresenceUpdate_WhenUserHasACustomStatus_ShouldDispatchItAsCustom()
    {
        await HandleAsync(NetCordPayloads.Presence(DiscordHost.GuildId, AliceId, UserStatusType.Online,
            new JsonUserActivity { Type = UserActivityType.Custom, Name = "Custom Status", State = "brb" }));

        var presence = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformPresenceUpdated>().Presence;
        presence.Activities.ShouldHaveSingleItem().Type.ShouldBe(PlatformActivityType.Custom);
    }

    [Fact]
    public async Task PresenceUpdate_WhenFromAnotherGuild_ShouldDispatchNothing()
    {
        await HandleAsync(NetCordPayloads.Presence(ForeignGuildId, AliceId, UserStatusType.Online));

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    private static PlatformRef Ref(ulong snowflake) => DiscordIds.ToRef(snowflake);

    private ValueTask HandleAsync(Presence presence) =>
        ((IPresenceUpdateGatewayHandler)new DiscordPresenceHandler(_harness.Mapper, _harness.Time)).HandleAsync(presence);
}
