using Apollo.Data.Tracking;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.Extensions.Logging;
using static Apollo.Bot.Tests.Tracking.TrackingCommandHost;

namespace Apollo.Bot.Tests.Tracking;

public sealed class TrackingOnCommandHandlerTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private const string NotSetUp = "Tracking ist noch nicht eingerichtet.";

    [Fact]
    public async Task On_WhenMemberLacksTheTrackingRole_ShouldAddItOnceAndExplainWhatIsRecordedWithPrivacyLink()
    {
        await using var host = await CreateInTrackingRoleModeAsync(Fixture);

        var reply = await host.RunAsync(Carol, "apollo tracking on");

        host.Platform.RoleChanges.ShouldBe([new FakeRoleChange(Carol, Statistik, FakeRoleChangeKind.Added)]);
        reply.Body().ShouldBe(
            "Ab jetzt werden deine Aktivitäten erfasst: Nachrichten-Metadaten, Voice-Zeit, Reaktionen und Spiele. "
            + "Nachrichteninhalte werden nie gespeichert.");
        reply.Text.ShouldEndWith($"{PublicWebUrl}/datenschutz");
    }

    [Fact]
    public async Task On_WhenMemberAlreadyHoldsTheTrackingRole_ShouldMakeNoRoleCallAndSaySo()
    {
        await using var host = await CreateInTrackingRoleModeAsync(Fixture);

        var reply = await host.RunAsync(Alice, "apollo tracking on");

        host.Platform.RoleChanges.ShouldBeEmpty();
        reply.Body().ShouldBe("Die Erfassung ist für dich bereits eingeschaltet.");
    }

    [Fact]
    public async Task On_WhenMemberTurnedTrackingOffBefore_ShouldAddTheRoleAgainAndStartANewPeriod()
    {
        await using var host = await CreateInTrackingRoleModeAsync(Fixture);
        await host.RunAsync(Alice, "apollo tracking off");
        host.Time.Advance(TimeSpan.FromDays(3));

        await host.RunAsync(Alice, "apollo tracking on");

        host.Platform.RoleChanges.ShouldBe(
        [
            new FakeRoleChange(Alice, Statistik, FakeRoleChangeKind.Removed),
            new FakeRoleChange(Alice, Statistik, FakeRoleChangeKind.Added),
        ]);
        var alice = await host.UserIdAsync(Alice);
        host.Gate.IsTracked(alice).ShouldBeTrue();
        var periods = (await host.PeriodsAsync()).Where(p => p.UserId == alice).ToList();
        periods.Count.ShouldBe(2);
        periods[^1].From.ShouldBe(TestTime.Now + TimeSpan.FromDays(3));
        periods[^1].To.ShouldBeNull();
    }

    [Fact]
    public async Task On_InBootstrapMode_ShouldSayNotSetUpAndMakeNoRoleCall()
    {
        await using var host = Create(Fixture);
        await host.SyncAsync(Snapshot());

        var reply = await host.RunAsync(Carol, "apollo tracking on");

        host.Platform.RoleChanges.ShouldBeEmpty();
        reply.Body().ShouldBe(NotSetUp);
    }

    [Fact]
    public async Task On_BeforeTheFirstGuildSync_ShouldSayNotSetUpAndMakeNoRoleCall()
    {
        await using var host = Create(Fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        host.Platform.WithMember(new PlatformMemberBuilder().ForUser(Carol).Build());

        var reply = await host.RunAsync(Carol, "apollo tracking on");

        host.Platform.RoleChanges.ShouldBeEmpty();
        reply.Body().ShouldBe(NotSetUp);
    }

    [Fact]
    public async Task On_WhenTrackingRoleWasDeleted_ShouldSayNotSetUpAndMakeNoRoleCall()
    {
        await using var host = Create(Fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        await host.SyncAsync(Snapshot(withTrackingRole: false));

        var reply = await host.RunAsync(Carol, "apollo tracking on");

        host.Platform.RoleChanges.ShouldBeEmpty();
        reply.Body().ShouldBe(NotSetUp);
    }

    [Fact]
    public async Task On_WhenTrackingRoleWasCleared_ShouldSayNotSetUpAndMakeNoRoleCall()
    {
        await using var host = Create(Fixture);
        var setting = await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        await RemoveAsync(host, setting);
        await host.SyncAsync(Snapshot());

        var reply = await host.RunAsync(Carol, "apollo tracking on");

        host.Platform.RoleChanges.ShouldBeEmpty();
        reply.Body().ShouldBe(NotSetUp);
    }

    [Fact]
    public async Task On_WhenThePlatformRefusesTheRoleChange_ShouldLogTheErrorAndSayTheChangeDidNotWork()
    {
        await using var host = await CreateInTrackingRoleModeAsync(Fixture);
        host.Platform.FailRoleChanges(new InvalidOperationException("Missing Permissions"));

        var reply = await host.RunAsync(Carol, "apollo tracking on");

        reply.Body().ShouldBe(
            "Das hat nicht geklappt: Deine Tracking-Rolle konnte nicht geändert werden. "
            + "Bitte versuche es später noch einmal oder melde dich bei einem Admin.");
        host.Logs.GetSnapshot().ShouldContain(r =>
            r.Level == LogLevel.Error && r.Exception is InvalidOperationException && r.Message.Contains("add the tracking role"));
    }

    internal static Task<int> RemoveAsync(TrackingHost host, RoleSetting setting) =>
        host.InScopeAsync(async (db, _) =>
        {
            db.RoleSettings.Remove(setting);
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
}
