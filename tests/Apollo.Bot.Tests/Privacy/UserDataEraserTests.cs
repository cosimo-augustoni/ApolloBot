using Apollo.Bot.Privacy;
using Apollo.Bot.Status;
using Apollo.Bot.Tests.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Apollo.Bot.Tests.Privacy.PrivacyCommandHost;
using static Apollo.Bot.Tests.Privacy.RecordedData;
using static Apollo.Bot.Tests.Tracking.TrackingCommandHost;

namespace Apollo.Bot.Tests.Privacy;

/// <summary>
/// The deletion behind the confirm button of <c>/apollo privacy delete</c>, end to end through the command router.
/// </summary>
public sealed class UserDataEraserTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private const string DeletedAndRoleRemoved =
        "Deine Daten wurden gelöscht und die Tracking-Rolle entfernt. Mit `/apollo tracking on` kannst du das Tracking "
        + "jederzeit wieder einschalten; es beginnt dann bei null.";

    private static readonly PlatformRef _lounge = TestPlatform.Channel("lounge");
    private static readonly TimeSpan _later = TimeSpan.FromMinutes(5);

    private static readonly string[] _ownTables =
    [
        "message_events",
        "message_mentions",
        "voice_sessions",
        "voice_state_segments",
        "reaction_events",
        "membership_events",
        "presence_activity_sessions",
        "tracking_periods",
    ];

    private static readonly string[] _eventTables = [.. _ownTables.Where(t => t != "tracking_periods")];

    [Fact]
    public async Task Confirm_WhenTrackedMemberHasRowsInEveryTable_ShouldLeaveNoneOfTheirRows()
    {
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture);
        await SeedEveryoneAsync(host);
        (await RowsAboutAsync(host, Alice)).Where(r => _ownTables.Contains(r.Key)).ShouldAllBe(r => r.Value > 0);

        await host.ConfirmDeleteAsync(Alice);

        (await RowsAboutAsync(host, Alice)).ShouldAllBe(r => r.Value == 0);
    }

    [Fact]
    public async Task Confirm_WhenOthersReferenceTheMember_ShouldClearThoseReferencesAndChangeNothingElseOfTheirs()
    {
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture);
        await SeedEveryoneAsync(host);
        var bobBefore = await RowsAboutAsync(host, Bob);
        var bobRowsBefore = await RowsWithoutReferencesAsync(host, Bob);
        var carolBefore = await RowsAboutAsync(host, Carol);

        await host.ConfirmDeleteAsync(Alice);

        var alice = await RowsAboutAsync(host, Alice);
        alice[ReplyReferences].ShouldBe(0);
        alice[MentionReferences].ShouldBe(0);
        alice[ReactionReferences].ShouldBe(0);
        (await RowsWithoutReferencesAsync(host, Bob)).ShouldBe(bobRowsBefore);

        // Alice's own rows referenced bob; carol's rows still do.
        var bobAfter = await RowsAboutAsync(host, Bob);
        bobAfter.ShouldBe(new Dictionary<string, int>(bobBefore)
        {
            [ReplyReferences] = bobBefore[ReplyReferences] - 1,
            [MentionReferences] = bobBefore[MentionReferences] - 1,
            [ReactionReferences] = bobBefore[ReactionReferences] - 1,
        });
        (await RowsAboutAsync(host, Carol)).ShouldBe(carolBefore);
    }

    [Fact]
    public async Task Confirm_WhenTheMemberHasOpenVoiceAndPresenceSessions_ShouldLeaveNoSessionRowsAlsoNoneWrittenByTheClose()
    {
        await using var host = CreatePrivacyHost(Fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        host.Platform
            .WithVoiceState(new PlatformVoiceStateBuilder().ForUser(Alice).InChannel(_lounge).Streaming().Build())
            .WithPresence(new PlatformPresenceBuilder().ForUser(Alice).Playing("Factorio").Build());
        await host.SyncAsync(Snapshot());
        host.Services.GetRequiredService<IOpenVoiceSessionCounter>().CountOpenSessions().ShouldBe(1);
        host.Time.Advance(_later);

        await host.ConfirmDeleteAsync(Alice);

        var rows = await RowsAboutAsync(host, Alice);
        rows["voice_sessions"].ShouldBe(0);
        rows["voice_state_segments"].ShouldBe(0);
        rows["presence_activity_sessions"].ShouldBe(0);
        host.Services.GetRequiredService<IOpenVoiceSessionCounter>().CountOpenSessions().ShouldBe(0);
    }

    [Fact]
    public async Task Confirm_WhenTheMemberHoldsTheTrackingRole_ShouldRemoveItStopTrackingAndPointToTrackingOn()
    {
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture);

        var reply = await host.ConfirmDeleteAsync(Alice);

        host.Platform.RoleChanges.ShouldBe([new FakeRoleChange(Alice, Statistik, FakeRoleChangeKind.Removed)]);
        host.Gate.IsTracked(await host.UserIdAsync(Alice)).ShouldBeFalse();
        reply.Body().ShouldBe(DeletedAndRoleRemoved);
        reply.Body().ShouldContain("`/apollo tracking on`");
        reply.Buttons.ShouldBeEmpty();
    }

    [Fact]
    public async Task Confirm_WhenConfirmedAgainBeforeTheRoleRemovalIsSynced_ShouldKeepTheMemberUntrackedWithoutNewRowsOrPeriod()
    {
        await using var host = CreatePrivacyHost(Fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        host.Platform.WithVoiceState(new PlatformVoiceStateBuilder().ForUser(Alice).InChannel(_lounge).Build());
        await host.SyncAsync(Snapshot());
        host.Platform.HoldMemberUpdates();
        await host.ConfirmDeleteAsync(Alice);
        host.Time.Advance(_later);

        var reply = await host.ConfirmDeleteAsync(Alice);

        var alice = await host.UserIdAsync(Alice);
        host.Gate.IsTracked(alice).ShouldBeFalse();
        (await RowsAboutAsync(host, Alice)).ShouldAllBe(r => r.Value == 0);
        reply.Body().ShouldBe(DeletedAndRoleRemoved);

        await host.Platform.ReleaseMemberUpdatesAsync(TestContext.Current.CancellationToken);

        host.Gate.IsTracked(alice).ShouldBeFalse();
        (await RowsAboutAsync(host, Alice)).ShouldAllBe(r => r.Value == 0);
    }

    [Fact]
    public async Task Erase_WhenCancelledAfterTheGateMark_ShouldReleaseTheMarkSoTheMemberIsTrackedAsBefore()
    {
        using var interceptor = new CancellingDeleteInterceptor();
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture, interceptor);
        await SeedAsync(host, Alice, Bob);

        await Should.ThrowAsync<OperationCanceledException>(() => host.InScopeAsync(async (_, _) =>
        {
            await using var scope = host.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IUserDataEraser>()
                .EraseAsync(TestPlatform.Guild, Alice, interceptor.Source.Token);
        }));

        host.Gate.IsTracked(await host.UserIdAsync(Alice)).ShouldBeTrue();
        (await RowsAboutAsync(host, Alice))["message_events"].ShouldBe(1);
        host.Platform.RoleChanges.ShouldBeEmpty();
    }

    [Fact]
    public async Task Confirm_WhenTheDeletionFailsMidway_ShouldRollBackKeepTheRoleAndReplyWithAGermanError()
    {
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture, new FailingDeleteInterceptor("tracking_periods"));
        await SeedEveryoneAsync(host);
        var before = await RowsAboutAsync(host, Alice);

        var reply = await host.ConfirmDeleteAsync(Alice);

        var after = await RowsAboutAsync(host, Alice);
        foreach (var table in _eventTables.Append(ReplyReferences).Append(MentionReferences).Append(ReactionReferences))
        {
            after[table].ShouldBe(before[table], table);
        }

        host.Platform.RoleChanges.ShouldBeEmpty();
        host.Gate.IsTracked(await host.UserIdAsync(Alice)).ShouldBeTrue();
        reply.Body().ShouldBe(
            "Das hat nicht geklappt: Deine Daten konnten nicht gelöscht werden, es wurde nichts gelöscht. "
            + "Bitte versuche es später noch einmal.");
        host.Logs.GetSnapshot().ShouldContain(r => r.Level == LogLevel.Error && r.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task Confirm_InBootstrapMode_ShouldDeleteTheAdministratorsDataMakeNoRoleCallAndSayTrackingIsNotSetUp()
    {
        await using var host = CreatePrivacyHost(Fixture);
        await host.SyncAsync(Snapshot());
        await SeedAsync(host, Bob, Alice);
        host.Time.Advance(_later);

        var reply = await host.ConfirmDeleteAsync(Bob);

        var rows = await RowsAboutAsync(host, Bob);
        _eventTables.ShouldAllBe(table => rows[table] == 0);
        host.Platform.RoleChanges.ShouldBeEmpty();
        reply.Body().ShouldBe(
            "Deine Daten wurden gelöscht. Tracking ist noch nicht eingerichtet: Bis eine Tracking-Rolle festgelegt ist, "
            + "werden Admins weiterhin erfasst.");

        // Recording continues while the bootstrap fallback applies, from a new period.
        var bob = await host.UserIdAsync(Bob);
        host.Gate.IsTracked(bob).ShouldBeTrue();
        var period = (await host.PeriodsAsync()).Where(p => p.UserId == bob).ShouldHaveSingleItem();
        period.From.ShouldBe(TestTime.Now + _later);
        period.To.ShouldBeNull();
    }

    [Fact]
    public async Task Confirm_ThenTrackingOn_ShouldRecordAgainFromANewPeriodWithoutAnyEarlierData()
    {
        await using var host = CreatePrivacyHost(Fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        host.Platform.WithVoiceState(new PlatformVoiceStateBuilder().ForUser(Alice).InChannel(_lounge).Build());
        await host.SyncAsync(Snapshot());
        await SeedAsync(host, Alice, Bob);
        await host.ConfirmDeleteAsync(Alice);
        host.Time.Advance(_later);

        await host.RunAsync(Alice, "apollo tracking on");

        var alice = await host.UserIdAsync(Alice);
        host.Gate.IsTracked(alice).ShouldBeTrue();
        var period = (await host.PeriodsAsync()).Where(p => p.UserId == alice).ShouldHaveSingleItem();
        period.From.ShouldBe(TestTime.Now + _later);
        period.To.ShouldBeNull();
        var rows = await RowsAboutAsync(host, Alice);
        rows.Where(r => r.Key is not "voice_sessions" and not "voice_state_segments" and not "tracking_periods").ShouldAllBe(r => r.Value == 0);
        rows["voice_sessions"].ShouldBe(1, "only the session opened by tracking on");
        rows["voice_state_segments"].ShouldBe(1);
    }

    [Fact]
    public async Task Confirm_WhenThePlatformRefusesToRemoveTheRole_ShouldStillDeleteAndSayTheMemberIsStillTracked()
    {
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture);
        await SeedAsync(host, Alice, Bob);
        host.Platform.FailRoleChanges(new InvalidOperationException("Missing Permissions"));

        var reply = await host.ConfirmDeleteAsync(Alice);

        var rows = await RowsAboutAsync(host, Alice);
        _eventTables.ShouldAllBe(table => rows[table] == 0);
        host.Gate.IsTracked(await host.UserIdAsync(Alice)).ShouldBeTrue();
        reply.Body().ShouldBe(
            "Deine Daten wurden gelöscht, aber deine Tracking-Rolle konnte nicht entfernt werden, deshalb werden weiter "
            + "Daten erfasst. Bitte melde dich bei einem Admin.");
        host.Logs.GetSnapshot().ShouldContain(r =>
            r.Level == LogLevel.Error && r.Exception is InvalidOperationException && r.Message.Contains("remove the tracking role"));
    }

    [Fact]
    public async Task Confirm_WhenTheMemberDoesNotHoldTheTrackingRole_ShouldDeleteWithoutARoleCallAndPointToTrackingOn()
    {
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture);
        await SeedAsync(host, Carol, Alice);

        var reply = await host.ConfirmDeleteAsync(Carol);

        (await RowsAboutAsync(host, Carol)).ShouldAllBe(r => r.Value == 0);
        host.Platform.RoleChanges.ShouldBeEmpty();
        reply.Body().ShouldBe(
            "Deine Daten wurden gelöscht. Mit `/apollo tracking on` kannst du das Tracking jederzeit einschalten; es "
            + "beginnt dann bei null.");
    }

    [Fact]
    public async Task Confirm_WhenTheTrackingRoleWasDeleted_ShouldDeleteWithoutARoleCallAndSayTrackingIsNotSetUp()
    {
        await using var host = CreatePrivacyHost(Fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        await host.SyncAsync(Snapshot(withTrackingRole: false));
        await SeedAsync(host, Alice, Bob);

        var reply = await host.ConfirmDeleteAsync(Alice);

        (await RowsAboutAsync(host, Alice)).ShouldAllBe(r => r.Value == 0);
        host.Platform.RoleChanges.ShouldBeEmpty();
        reply.Body().ShouldBe("Deine Daten wurden gelöscht. Tracking ist noch nicht eingerichtet.");
    }

    [Fact]
    public async Task Confirm_WhenTheMemberWasNeverSeen_ShouldCreateNoUserAndMakeNoRoleCall()
    {
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture);
        var stranger = TestPlatform.User("stranger");

        await host.ConfirmDeleteAsync(stranger);

        (await host.InScopeAsync((_, ids) => ids.TryGetUserAsync(stranger, TestContext.Current.CancellationToken))).ShouldBeNull();
        host.Platform.RoleChanges.ShouldBeEmpty();
    }

    [Fact]
    public async Task Confirm_ShouldLogTheDeletedAndClearedRowsPerTableWithoutPlatformIds()
    {
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture);
        await SeedEveryoneAsync(host);
        var alice = await host.UserIdAsync(Alice);

        await host.ConfirmDeleteAsync(Alice);

        var record = host.Logs.GetSnapshot().Where(r => r.Message.StartsWith("Erased the data of", StringComparison.Ordinal)).ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Information);
        var state = record.StructuredState!.ToDictionary(kv => kv.Key, kv => kv.Value);
        state["UserId"].ShouldBe(alice.ToString());
        state["DeletedRows"].ShouldBe("9", "one row per event table plus the seeded and the open tracking period");
        state["ClearedReferences"].ShouldBe("3");
        state["Tables"].ShouldBe(
            "message_events: 1 deleted, 1 cleared; message_mentions: 1 deleted, 1 cleared; voice_sessions: 1 deleted; "
            + "voice_state_segments: 1 deleted; reaction_events: 1 deleted, 1 cleared; membership_events: 1 deleted; "
            + "presence_activity_sessions: 1 deleted; tracking_periods: 2 deleted");
        record.Message.ShouldNotContain(Alice.ExternalId);
    }

    /// <summary>
    /// Alice's rows reference bob, bob's reference alice and carol's reference bob.
    /// </summary>
    private static async Task SeedEveryoneAsync(TrackingHost host)
    {
        await SeedAsync(host, Alice, Bob);
        await SeedAsync(host, Bob, Alice);
        await SeedAsync(host, Carol, Bob);
    }
}
