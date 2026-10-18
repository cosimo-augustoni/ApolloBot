using Apollo.Bot.Privacy;
using Apollo.Bot.Tests.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Apollo.Bot.Tests.Privacy.PrivacyCommandHost;
using static Apollo.Bot.Tests.Privacy.RecordedData;
using static Apollo.Bot.Tests.Tracking.TrackingCommandHost;

namespace Apollo.Bot.Tests.Privacy;

/// <summary>
/// The aggregate refresh after a confirmed <c>/apollo privacy delete</c> (#39). The seeded rows are months older than the
/// database's clock, so no refresh policy re-materializes them: only the deletion's own refresh can. Aggregates are not
/// reset between tests, so every read is limited to the guild of the test (a new internal ID after every reset).
/// </summary>
public sealed class PrivacyDeleteAggregateTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private const string DeletedAndRoleRemoved =
        "Deine Daten wurden gelöscht und die Tracking-Rolle entfernt. Mit `/apollo tracking on` kannst du das Tracking "
        + "jederzeit wieder einschalten; es beginnt dann bei null.";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Confirm_WhenTheMemberHasMaterializedOldEvents_ShouldLeaveNoAggregateRowOfTheirsAndLowerTheGuildTotals()
    {
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture);
        await SeedAsync(host, Alice, Carol);
        await SeedAsync(host, Bob, Carol);
        await Fixture.RefreshContinuousAggregatesAsync(Ct);
        var before = await AggregatesAsync(host);
        before.Activity.ShouldContain(a => IsOf(a, Alice));
        before.Heatmap.ShouldContain(h => IsOf(h, Alice));
        before.GuildTotals.ShouldBe(["messages 2, characters 84, reactions 2"]);

        await host.ConfirmDeleteAsync(Alice);

        var after = await AggregatesAsync(host);
        after.Activity.ShouldNotContain(a => IsOf(a, Alice));
        after.Heatmap.ShouldNotContain(h => IsOf(h, Alice));
        after.GuildTotals.ShouldBe(["messages 1, characters 42, reactions 1"], "bob's message and reaction are left");
    }

    [Fact]
    public async Task Confirm_ShouldLeaveTheAggregatesOfOtherMembersOnTheSameDaysUnchanged()
    {
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture);

        // Alice reacts to her own message only: a reaction of hers on carol's message is her data, and carol's
        // "received" would rightly drop with it.
        await SeedAsync(host, Alice, Alice);
        await SeedAsync(host, Bob, Carol);
        await Fixture.RefreshContinuousAggregatesAsync(Ct);
        var before = await AggregatesAsync(host);
        before.Activity.ShouldContain(a => IsOf(a, Bob));
        before.Activity.ShouldContain(a => IsOf(a, Carol));

        await host.ConfirmDeleteAsync(Alice);

        var after = await AggregatesAsync(host);
        after.Activity.ShouldBe(before.Activity.Where(a => !IsOf(a, Alice)));
        after.Heatmap.ShouldBe(before.Heatmap.Where(h => !IsOf(h, Alice)));
    }

    [Fact]
    public async Task Confirm_WhenOthersReactedToTheMembersMessages_ShouldDropTheReactionsTheyReceived()
    {
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture);
        await SeedAsync(host, Carol, Alice);
        await Fixture.RefreshContinuousAggregatesAsync(Ct);
        (await AggregatesAsync(host)).Activity.ShouldContain(a => IsOf(a, Alice) && a.Contains("received 1", StringComparison.Ordinal));

        await host.ConfirmDeleteAsync(Alice);

        var after = await AggregatesAsync(host);
        after.Activity.ShouldNotContain(a => IsOf(a, Alice));
        after.Activity.ShouldContain(a => IsOf(a, Carol) && a.Contains("given 1", StringComparison.Ordinal), "carol's reaction stays");
    }

    [Fact]
    public async Task Confirm_ShouldRefreshAfterTheCommitOverEveryRowOfTheMemberAndEveryReferenceToThem()
    {
        var refreshes = new AggregateRefreshLog();
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture, configure: RecordingAggregateRefresher.Replace(refreshes));
        var first = TestTime.Now - TimeSpan.FromDays(40);
        var last = TestTime.Now - TimeSpan.FromDays(10);
        await SeedAsync(host, Carol, Alice, first);
        await SeedAsync(host, Alice, Bob, last);
        await SeedAsync(host, Bob, Carol, first - TimeSpan.FromDays(1));

        await host.ConfirmDeleteAsync(Alice);

        refreshes.Calls.ShouldBe([new AggregateRefreshLog.Call(first, last, InTransaction: false)]);
    }

    [Fact]
    public async Task Confirm_WhenTheMemberHasNoEvents_ShouldDeleteWithoutARefresh()
    {
        var refreshes = new AggregateRefreshLog();
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture, configure: RecordingAggregateRefresher.Replace(refreshes));
        await SeedAsync(host, Bob, Carol);

        var reply = await host.ConfirmDeleteAsync(Alice);

        reply.Body().ShouldBe(DeletedAndRoleRemoved);
        refreshes.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Confirm_WhenTheRefreshFails_ShouldKeepTheDeletionRemoveTheRoleAndLogTheError()
    {
        var refreshes = new AggregateRefreshLog { Failure = new InvalidOperationException("refresh failed") };
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture, configure: RecordingAggregateRefresher.Replace(refreshes));
        await SeedAsync(host, Alice, Bob);

        var reply = await host.ConfirmDeleteAsync(Alice);

        reply.Body().ShouldBe(DeletedAndRoleRemoved);
        (await RowsAboutAsync(host, Alice)).ShouldAllBe(r => r.Value == 0);
        host.Platform.RoleChanges.ShouldBe([new FakeRoleChange(Alice, Statistik, FakeRoleChangeKind.Removed)]);
        var record = host.Logs.GetSnapshot().Where(r => r.Level == LogLevel.Error).ShouldHaveSingleItem();
        record.Exception.ShouldBe(refreshes.Failure);
        record.StructuredState!.ToDictionary(kv => kv.Key, kv => kv.Value)["UserId"].ShouldBe((await host.UserIdAsync(Alice)).ToString());
    }

    /// <summary>
    /// A shutdown while the refresh runs must not skip the role removal: the data is already deleted, and without the
    /// removal the member, who still holds the tracking role, would be recorded again.
    /// </summary>
    [Fact]
    public async Task Erase_WhenCancelledDuringTheRefresh_ShouldHaveRemovedTheRoleAndKeepTheMemberUntracked()
    {
        using var cancellation = new CancellationTokenSource();
        var refreshes = new AggregateRefreshLog
        {
            During = () =>
            {
                cancellation.Cancel();
                cancellation.Token.ThrowIfCancellationRequested();
            },
        };
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture, configure: RecordingAggregateRefresher.Replace(refreshes));
        await SeedAsync(host, Alice, Bob);

        await Should.ThrowAsync<OperationCanceledException>(() => host.InScopeAsync(async (_, _) =>
        {
            await using var scope = host.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IUserDataEraser>()
                .EraseAsync(TestPlatform.Guild, Alice, cancellation.Token);
        }));

        refreshes.Calls.ShouldHaveSingleItem();
        host.Platform.RoleChanges.ShouldBe([new FakeRoleChange(Alice, Statistik, FakeRoleChangeKind.Removed)]);
        host.Gate.IsTracked(await host.UserIdAsync(Alice)).ShouldBeFalse();
    }

    [Fact]
    public async Task Confirm_ShouldLogTheRefreshedRangeWithTheInternalUserIdOnly()
    {
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture);
        await SeedAsync(host, Alice, Bob);

        await host.ConfirmDeleteAsync(Alice);

        var record = host.Logs.GetSnapshot().Where(r => r.Message.StartsWith("Refreshed the aggregates after erasing", StringComparison.Ordinal)).ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Information);
        var state = record.StructuredState!.ToDictionary(kv => kv.Key, kv => kv.Value);
        state["UserId"].ShouldBe((await host.UserIdAsync(Alice)).ToString());
        state.ShouldContainKey("From");
        state.ShouldContainKey("To");
        record.Message.ShouldNotContain(Alice.ExternalId);
    }

    private static bool IsOf(string row, PlatformRef member) => row.StartsWith($"{member.ExternalId}:", StringComparison.Ordinal);

    /// <summary>
    /// The aggregate rows of the test's guild as text, each starting with the member's platform ID.
    /// </summary>
    private static Task<Aggregates> AggregatesAsync(TrackingHost host) =>
        host.InScopeAsync(async (db, ids) =>
        {
            var guildId = await ids.TryGetGuildAsync(TestPlatform.Guild, Ct) ?? throw new InvalidOperationException("The guild was never resolved.");
            var members = new Dictionary<Guid, string>();
            foreach (var member in new[] { Alice, Bob, Carol })
            {
                if (await ids.TryGetUserAsync(member, Ct) is { } id)
                {
                    members[id] = member.ExternalId;
                }
            }

            string Name(Guid id) => members.TryGetValue(id, out var name) ? name : $"{id}";

            var activity = await db.DailyUserChannelActivity.AsNoTracking().Where(a => a.GuildId == guildId)
                .OrderBy(a => a.Day).ThenBy(a => a.UserId).ThenBy(a => a.ChannelId).ToListAsync(Ct);
            var heatmap = await db.HourlyActivityHeatmap.AsNoTracking().Where(h => h.GuildId == guildId)
                .OrderBy(h => h.Hour).ThenBy(h => h.UserId).ToListAsync(Ct);
            var guild = await db.DailyGuildActivity.AsNoTracking().Where(g => g.GuildId == guildId && !g.IsBot)
                .OrderBy(g => g.Day).ToListAsync(Ct);
            return new Aggregates(
                [.. activity.Select(a => $"{Name(a.UserId)}: {a.Day:o} {a.ChannelId} messages {a.MessageCount}, characters {a.CharacterCount}, given {a.ReactionsGiven}, received {a.ReactionsReceived}, voice {a.VoiceSeconds}")],
                [.. heatmap.Select(h => $"{Name(h.UserId)}: {h.Hour:o} messages {h.MessageCount}")],
                [.. guild.Select(g => $"messages {g.MessageCount}, characters {g.CharacterCount}, reactions {g.ReactionCount}")]);
        });

    private sealed record Aggregates(List<string> Activity, List<string> Heatmap, List<string> GuildTotals);
}
