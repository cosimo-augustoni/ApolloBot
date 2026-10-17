using Apollo.Bot.Commands;
using Apollo.Bot.Links;
using Apollo.Bot.Stats;
using Apollo.Bot.Tests.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Commands;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.Extensions.DependencyInjection;
using static Apollo.Bot.Tests.Tracking.TrackingCommandHost;

namespace Apollo.Bot.Tests.Stats;

/// <summary>
/// <c>/apollo stats</c> next to the real tracking gate: the reply does not depend on whether, or how, a member is
/// tracked.
/// </summary>
public sealed class StatsCommandTrackingTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private const string Expected = $"Deine Statistiken findest du hier: {PublicWebUrl}/me";

    private static readonly PlatformRef _stranger = TestPlatform.User("stranger");

    [Fact]
    public async Task Stats_WhenMemberIsTrackedUntrackedOrUnknownInTrackingRoleMode_ShouldReplyTheSame()
    {
        await using var host = CreateHost();
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        await host.SyncAsync(Snapshot());

        var replies = await RunForAsync(host, Alice, Carol, _stranger);

        host.Gate.IsTracked(await host.UserIdAsync(Alice)).ShouldBeTrue();
        host.Gate.IsTracked(await host.UserIdAsync(Carol)).ShouldBeFalse();
        replies.ShouldAllBe(reply => reply.Visibility == ReplyVisibility.Private && reply.Body() == Expected);
    }

    [Fact]
    public async Task Stats_WhenNoTrackingRoleIsSet_ShouldReplyTheSameToAdministratorsAndOtherMembers()
    {
        await using var host = CreateHost();
        await host.SyncAsync(Snapshot(withTrackingRole: false));

        var replies = await RunForAsync(host, Bob, Carol, _stranger);

        host.Gate.IsTracked(await host.UserIdAsync(Bob)).ShouldBeFalse();
        host.Gate.IsTracked(await host.UserIdAsync(Carol)).ShouldBeFalse();
        replies.ShouldAllBe(reply => reply.Visibility == ReplyVisibility.Private && reply.Body() == Expected);
    }

    [Fact]
    public async Task Stats_ShouldCreateNoDataForAMemberTheBotHasNeverSeen()
    {
        await using var host = CreateHost();

        await host.RunAsync(_stranger, "apollo stats");

        (await host.InScopeAsync((_, ids) => ids.TryGetUserAsync(_stranger, TestContext.Current.CancellationToken))).ShouldBeNull();
    }

    private TrackingHost CreateHost()
    {
        var host = new TrackingHost(Fixture, services => services
            .AddCommandRouting()
            .Configure<LinksOptions>(links => links.PublicWebUrl = PublicWebUrl)
            .AddStatsCommands());
        host.Platform.WithCommandRouter(host.Services.GetRequiredService<ICommandRouter>());
        return host;
    }

    private static async Task<List<CommandReply>> RunForAsync(TrackingHost host, params PlatformRef[] members)
    {
        var replies = new List<CommandReply>();
        foreach (var member in members)
        {
            replies.Add(await host.RunAsync(member, "apollo stats"));
        }

        return replies;
    }
}
