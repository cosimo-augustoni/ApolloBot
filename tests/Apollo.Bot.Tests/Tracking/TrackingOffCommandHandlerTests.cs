using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.Extensions.Logging;
using static Apollo.Bot.Tests.Tracking.TrackingCommandHost;

namespace Apollo.Bot.Tests.Tracking;

public sealed class TrackingOffCommandHandlerTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private const string NotSetUp = "Tracking ist noch nicht eingerichtet.";

    [Fact]
    public async Task Off_WhenMemberHoldsTheTrackingRole_ShouldRemoveItOnceAndReplyWithTheExactScopeText()
    {
        await using var host = await CreateInTrackingRoleModeAsync(Fixture);

        var reply = await host.RunAsync(Alice, "apollo tracking off");

        host.Platform.RoleChanges.ShouldBe([new FakeRoleChange(Alice, Statistik, FakeRoleChangeKind.Removed)]);
        reply.Body().ShouldBe(
            "Ab jetzt werden keine Daten mehr erfasst. Bereits erfasste Daten bleiben gespeichert. "
            + "Mit `/apollo privacy delete` kannst du sie endgültig löschen.");
        reply.Text.ShouldEndWith($"{PublicWebUrl}/datenschutz");
    }

    [Fact]
    public async Task Off_WhenMemberLacksTheTrackingRole_ShouldMakeNoRoleCallAndSayNoDataIsRecorded()
    {
        await using var host = await CreateInTrackingRoleModeAsync(Fixture);

        var reply = await host.RunAsync(Carol, "apollo tracking off");

        host.Platform.RoleChanges.ShouldBeEmpty();
        reply.Body().ShouldBe("Für dich werden keine Daten erfasst. Mit `/apollo tracking on` kannst du die Erfassung einschalten.");
    }

    [Fact]
    public async Task Off_WhenTrackedMemberRunsIt_ShouldStopTrackingThroughTheEchoedMemberUpdate()
    {
        await using var host = await CreateInTrackingRoleModeAsync(Fixture);
        var alice = await host.UserIdAsync(Alice);
        host.Time.Advance(TimeSpan.FromHours(2));

        await host.RunAsync(Alice, "apollo tracking off");

        host.Gate.IsTracked(alice).ShouldBeFalse();
        host.Lifecycle.Stopped.ShouldBe([alice]);
        var period = (await host.PeriodsAsync()).Where(p => p.UserId == alice).ShouldHaveSingleItem();
        period.From.ShouldBe(TestTime.Now);
        period.To.ShouldBe(TestTime.Now + TimeSpan.FromHours(2));
    }

    [Fact]
    public async Task Off_WhenNoTrackingRoleIsSet_ShouldSayNotSetUpToAnAdministratorAndMakeNoRoleCall()
    {
        await using var host = Create(Fixture);
        await host.SyncAsync(Snapshot());

        var reply = await host.RunAsync(Bob, "apollo tracking off");

        host.Platform.RoleChanges.ShouldBeEmpty();
        reply.Body().ShouldBe(NotSetUp);
    }

    [Fact]
    public async Task Off_WhenTrackingRoleWasDeleted_ShouldSayNotSetUpAndMakeNoRoleCall()
    {
        await using var host = Create(Fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        await host.SyncAsync(Snapshot(withTrackingRole: false));

        var reply = await host.RunAsync(Alice, "apollo tracking off");

        host.Platform.RoleChanges.ShouldBeEmpty();
        reply.Body().ShouldBe(NotSetUp);
    }

    [Fact]
    public async Task Off_WhenTrackingRoleWasCleared_ShouldSayNotSetUpAndMakeNoRoleCall()
    {
        await using var host = Create(Fixture);
        var setting = await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        await TrackingOnCommandHandlerTests.RemoveAsync(host, setting);
        await host.SyncAsync(Snapshot());

        var reply = await host.RunAsync(Alice, "apollo tracking off");

        host.Platform.RoleChanges.ShouldBeEmpty();
        reply.Body().ShouldBe(NotSetUp);
    }

    [Fact]
    public async Task Off_WhenThePlatformRefusesTheRoleChange_ShouldLogTheErrorAndSayTheChangeDidNotWork()
    {
        await using var host = await CreateInTrackingRoleModeAsync(Fixture);
        host.Platform.FailRoleChanges(new InvalidOperationException("Missing Permissions"));

        var reply = await host.RunAsync(Alice, "apollo tracking off");

        reply.Body().ShouldBe(
            "Das hat nicht geklappt: Deine Tracking-Rolle konnte nicht geändert werden. "
            + "Bitte versuche es später noch einmal oder melde dich bei einem Admin.");
        host.Logs.GetSnapshot().ShouldContain(r =>
            r.Level == LogLevel.Error && r.Exception is InvalidOperationException && r.Message.Contains("remove the tracking role"));
    }
}
