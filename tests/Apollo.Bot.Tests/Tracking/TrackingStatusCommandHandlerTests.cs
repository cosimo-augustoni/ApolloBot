using Apollo.Data.Membership;
using Apollo.Data.Messages;
using Apollo.Data.Presence;
using Apollo.Data.Reactions;
using Apollo.Data.Voice;
using Apollo.Domain.Presence;
using Apollo.Domain.Voice;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using static Apollo.Bot.Tests.Tracking.TrackingCommandHost;

namespace Apollo.Bot.Tests.Tracking;

public sealed class TrackingStatusCommandHandlerTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    [Fact]
    public async Task Status_WhenMemberIsTracked_ShouldStateTrackedSinceInZurichAndCountsPerEventTable()
    {
        await using var host = Create(Fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        // 23:30 UTC on 15 January is already 16 January in Zurich.
        host.Time.Advance(TimeSpan.FromHours(14.5));
        await host.SyncAsync(Snapshot());
        await SeedEventsAsync(host, Alice, messages: 2, reactions: 1, voiceSessions: 3, presenceSessions: 1, membershipEvents: 0);
        await SeedEventsAsync(host, Carol, messages: 5, reactions: 5, voiceSessions: 5, presenceSessions: 5, membershipEvents: 5);

        var reply = await host.RunAsync(Alice, "apollo tracking status");

        reply.Body().ShouldBe(
            """
            Für dich werden seit dem 16.01.2026 Daten erfasst.

            Gespeicherte Einträge von dir:
            - Nachrichten: 2
            - Reaktionen: 1
            - Voice-Sitzungen: 3
            - Aktivitäten (z. B. Spiele): 1
            - Beitritte und Austritte: 0
            """.ReplaceLineEndings("\n"));
        reply.Text.ShouldEndWith($"{PublicWebUrl}/datenschutz");
    }

    [Fact]
    public async Task Status_WhenMemberIsNotTracked_ShouldSayNoDataIsRecordedAndStillShowEarlierCounts()
    {
        await using var host = await CreateInTrackingRoleModeAsync(Fixture);
        await SeedEventsAsync(host, Alice, messages: 1234, reactions: 0, voiceSessions: 0, presenceSessions: 0, membershipEvents: 2);
        await host.RunAsync(Alice, "apollo tracking off");

        var reply = await host.RunAsync(Alice, "apollo tracking status");

        reply.Body().ShouldBe(
            """
            Für dich werden keine Daten erfasst. Mit `/apollo tracking on` kannst du die Erfassung einschalten.

            Gespeicherte Einträge von dir:
            - Nachrichten: 1'234
            - Reaktionen: 0
            - Voice-Sitzungen: 0
            - Aktivitäten (z. B. Spiele): 0
            - Beitritte und Austritte: 2
            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Status_WhenNoDataIsStored_ShouldSaySo()
    {
        await using var host = await CreateInTrackingRoleModeAsync(Fixture);

        var reply = await host.RunAsync(Carol, "apollo tracking status");

        reply.Body().ShouldBe(
            """
            Für dich werden keine Daten erfasst. Mit `/apollo tracking on` kannst du die Erfassung einschalten.

            Es sind keine Daten von dir gespeichert.
            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Status_InBootstrapMode_ShouldReportTheGateResultAndThatTrackingIsNotSetUp()
    {
        await using var host = Create(Fixture);
        await host.SyncAsync(Snapshot());

        var admin = await host.RunAsync(Bob, "apollo tracking status");
        var member = await host.RunAsync(Carol, "apollo tracking status");

        admin.Body().ShouldBe(
            """
            Für dich werden seit dem 15.01.2026 Daten erfasst. Tracking ist noch nicht eingerichtet: Bis eine Tracking-Rolle festgelegt ist, werden nur Admins erfasst.

            Es sind keine Daten von dir gespeichert.
            """.ReplaceLineEndings("\n"));
        member.Body().ShouldBe(
            """
            Für dich werden keine Daten erfasst. Tracking ist noch nicht eingerichtet: Bis eine Tracking-Rolle festgelegt ist, werden nur Admins erfasst.

            Es sind keine Daten von dir gespeichert.
            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Status_WhenTrackingRoleWasDeleted_ShouldSayNotTrackedAndNotSetUp()
    {
        await using var host = Create(Fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        await host.SyncAsync(Snapshot(withTrackingRole: false));

        var reply = await host.RunAsync(Alice, "apollo tracking status");

        reply.Body().ShouldBe(
            """
            Für dich werden keine Daten erfasst. Tracking ist noch nicht eingerichtet.

            Es sind keine Daten von dir gespeichert.
            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Status_WhenTheMemberWasNeverSeen_ShouldSayNotTrackedWithoutCreatingAUser()
    {
        await using var host = await CreateInTrackingRoleModeAsync(Fixture);
        var stranger = TestPlatform.User("stranger");

        var reply = await host.RunAsync(stranger, "apollo tracking status");

        reply.Body().ShouldBe(
            """
            Für dich werden keine Daten erfasst. Mit `/apollo tracking on` kannst du die Erfassung einschalten.

            Es sind keine Daten von dir gespeichert.
            """.ReplaceLineEndings("\n"));
        (await host.InScopeAsync((_, ids) => ids.TryGetUserAsync(stranger, TestContext.Current.CancellationToken))).ShouldBeNull();
    }

    private static Task<int> SeedEventsAsync(
        TrackingHost host,
        PlatformRef user,
        int messages,
        int reactions,
        int voiceSessions,
        int presenceSessions,
        int membershipEvents) =>
        host.InScopeAsync(async (db, ids) =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var guildId = await ids.ResolveGuildAsync(TestPlatform.Guild, cancellationToken);
            var userId = await ids.ResolveUserAsync(user, cancellationToken);
            var channelId = await ids.ResolveChannelAsync(TestPlatform.Guild, TestPlatform.Channel("general"), cancellationToken);
            var at = TestTime.Now - TimeSpan.FromDays(1);

            db.MessageEvents.AddRange(Enumerable.Range(0, messages).Select(i => new MessageEvent
            {
                GuildId = guildId,
                UserId = userId,
                ChannelId = channelId,
                OccurredAt = at.AddSeconds(i),
                MessageExtId = $"m{i}",
                Kind = MessageEventKind.Create,
            }));
            db.ReactionEvents.AddRange(Enumerable.Range(0, reactions).Select(i => new ReactionEvent
            {
                GuildId = guildId,
                UserId = userId,
                ChannelId = channelId,
                OccurredAt = at.AddSeconds(i),
                MessageExtId = $"m{i}",
                EmojiName = "👍",
                Kind = ReactionEventKind.Add,
            }));
            db.VoiceSessions.AddRange(Enumerable.Range(0, voiceSessions).Select(i => new VoiceSession
            {
                GuildId = guildId,
                UserId = userId,
                ChannelId = channelId,
                OccurredAt = at.AddMinutes(i),
                EndedAt = at.AddMinutes(i).AddSeconds(30),
                EndReason = VoiceSessionEndReason.Left,
            }));
            db.PresenceActivitySessions.AddRange(Enumerable.Range(0, presenceSessions).Select(i => new PresenceActivitySession
            {
                GuildId = guildId,
                UserId = userId,
                OccurredAt = at.AddHours(i),
                EndedAt = at.AddHours(i).AddMinutes(30),
                ActivityType = PresenceActivityType.Playing,
                ActivityName = "Factorio",
            }));
            db.MembershipEvents.AddRange(Enumerable.Range(0, membershipEvents).Select(i => new MembershipEvent
            {
                GuildId = guildId,
                UserId = userId,
                OccurredAt = at.AddSeconds(i),
                Kind = MembershipEventKind.Join,
            }));
            return await db.SaveChangesAsync(cancellationToken);
        });
}
