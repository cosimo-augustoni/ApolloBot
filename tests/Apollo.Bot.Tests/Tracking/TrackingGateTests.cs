using Apollo.Bot.Tracking;
using Apollo.Data.Tracking;
using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Apollo.Bot.Tests.Tracking;

public sealed class TrackingGateTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly PlatformRef _statistik = TestPlatform.Role("statistik");
    private static readonly PlatformRef _admin = TestPlatform.Role("admin");
    private static readonly PlatformRef _mods = TestPlatform.Role("mods");

    // alice holds the tracking role, bob is an Administrator without it, carol has neither.
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _bob = TestPlatform.User("bob");
    private static readonly PlatformRef _carol = TestPlatform.User("carol");

    private static readonly TimeSpan _later = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task Gate_WhenNoTrackingRowExists_ShouldTrackAdministratorsOnly()
    {
        await using var host = new TrackingHost(Fixture);

        await host.SyncAsync(Snapshot());

        host.Gate.Mode.ShouldBe(TrackingMode.Bootstrap);
        host.Gate.IsTracked(await host.UserIdAsync(_bob)).ShouldBeTrue();
        host.Gate.IsTracked(await host.UserIdAsync(_alice)).ShouldBeFalse();
        host.Gate.IsTracked(await host.UserIdAsync(_carol)).ShouldBeFalse();
    }

    [Fact]
    public async Task Gate_WhenTrackingRowExists_ShouldTrackExactlyTheHoldersOfTheRole()
    {
        await using var host = new TrackingHost(Fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));

        await host.SyncAsync(Snapshot());

        host.Gate.Mode.ShouldBe(TrackingMode.TrackingRole);
        host.Gate.IsTracked(await host.UserIdAsync(_alice)).ShouldBeTrue();
        host.Gate.IsTracked(await host.UserIdAsync(_bob)).ShouldBeFalse();
        host.Gate.IsTracked(await host.UserIdAsync(_carol)).ShouldBeFalse();
    }

    [Fact]
    public async Task Gate_WhenTrackingRoleWasDeleted_ShouldTrackNobodyAndLogAWarning()
    {
        await using var host = new TrackingHost(Fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));

        // The snapshot no longer lists the tracking role, so the sync marks it deleted.
        await host.SyncAsync(Snapshot(withTrackingRole: false));

        host.Gate.Mode.ShouldBe(TrackingMode.TrackingRoleDeleted);
        host.Gate.IsTracked(await host.UserIdAsync(_alice)).ShouldBeFalse();
        host.Gate.IsTracked(await host.UserIdAsync(_bob)).ShouldBeFalse();
        host.Logs.GetSnapshot().ShouldContain(r => r.Level == LogLevel.Warning && r.Message.Contains("was deleted"));
    }

    [Fact]
    public async Task Gate_WhenTrackingRowWasRemoved_ShouldTrackNobodyNotEvenAdministratorsAndLogAWarning()
    {
        await using var host = new TrackingHost(Fixture);
        var setting = await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));
        await host.InScopeAsync(async (db, _) =>
        {
            db.RoleSettings.Remove(setting);
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await host.SyncAsync(Snapshot());

        host.Gate.Mode.ShouldBe(TrackingMode.TrackingRoleCleared);
        host.Gate.IsTracked(await host.UserIdAsync(_alice)).ShouldBeFalse();
        host.Gate.IsTracked(await host.UserIdAsync(_bob)).ShouldBeFalse();
        host.Logs.GetSnapshot().ShouldContain(r => r.Level == LogLevel.Warning && r.Message.Contains("was removed"));
    }

    [Fact]
    public async Task MemberChange_WhenMemberGainsTheTrackingRole_ShouldStartTrackingOnceAndOpenAPeriod()
    {
        await using var host = await HostInTrackingRoleModeAsync();
        var carol = await host.UserIdAsync(_carol);
        host.Time.Advance(_later);

        await host.RaiseAsync(Updated(_carol, _statistik));

        host.Gate.IsTracked(carol).ShouldBeTrue();
        host.Lifecycle.Started.ShouldBe([carol]);
        host.Lifecycle.Stopped.ShouldBeEmpty();
        var period = (await host.PeriodsAsync()).Where(p => p.UserId == carol).ShouldHaveSingleItem();
        period.From.ShouldBe(TestTime.Now + _later);
        period.To.ShouldBeNull();
    }

    [Fact]
    public async Task MemberChange_WhenTrackedMemberLosesTheTrackingRole_ShouldStopTrackingOnceAndClosePeriod()
    {
        await using var host = await HostInTrackingRoleModeAsync();
        var alice = await host.UserIdAsync(_alice);
        host.Time.Advance(_later);

        await host.RaiseAsync(Updated(_alice, _mods));

        host.Gate.IsTracked(alice).ShouldBeFalse();
        host.Lifecycle.Stopped.ShouldBe([alice]);
        host.Lifecycle.Started.ShouldBeEmpty();
        var period = (await host.PeriodsAsync()).Where(p => p.UserId == alice).ShouldHaveSingleItem();
        period.From.ShouldBe(TestTime.Now);
        period.To.ShouldBe(TestTime.Now + _later);
    }

    [Fact]
    public async Task MemberChange_WhenTrackedMemberLeavesTheGuild_ShouldStopTrackingAndClosePeriod()
    {
        await using var host = await HostInTrackingRoleModeAsync();
        var alice = await host.UserIdAsync(_alice);
        host.Time.Advance(_later);

        await host.RaiseAsync(new PlatformMemberLeftBuilder().ForUser(_alice).Build());

        host.Gate.IsTracked(alice).ShouldBeFalse();
        host.Lifecycle.Stopped.ShouldBe([alice]);
        (await host.PeriodsAsync()).Single(p => p.UserId == alice).To.ShouldBe(TestTime.Now + _later);
    }

    [Fact]
    public async Task MemberChange_WhenTrackingDoesNotChange_ShouldCallNoHook()
    {
        await using var host = await HostInTrackingRoleModeAsync();

        await host.RaiseAsync(Updated(_alice, _statistik, _mods));

        host.Lifecycle.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task MemberChange_WhenALifecycleHandlerThrows_ShouldStillCallTheOtherHandlersAndLogTheError()
    {
        await using var host = new TrackingHost(Fixture, services => services.AddTrackingLifecycleHandler<ThrowingLifecycleHandler>());
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));
        await host.SyncAsync(Snapshot());
        host.Lifecycle.Clear();
        var carol = await host.UserIdAsync(_carol);

        await host.RaiseAsync(Updated(_carol, _statistik));

        host.Lifecycle.Started.ShouldBe([carol]);
        host.Gate.IsTracked(carol).ShouldBeTrue();
        host.Logs.GetSnapshot().ShouldContain(r => r.Level == LogLevel.Error && r.Message.Contains(nameof(ThrowingLifecycleHandler)));
    }

    [Fact]
    public async Task Reload_WhenTrackingRowIsInsertedInBootstrapMode_ShouldSwitchToTheRoleHoldersAfter30Seconds()
    {
        await using var host = new TrackingHost(Fixture);
        await host.SyncAsync(Snapshot());
        var (alice, bob) = (await host.UserIdAsync(_alice), await host.UserIdAsync(_bob));
        host.Lifecycle.Clear();
        await host.StartReloaderAsync();
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));

        host.Time.Advance(TimeSpan.FromSeconds(30));
        await TrackingHost.WaitUntilAsync(() => host.Lifecycle.All.Count == 2, "the reload switched to the tracking role");

        host.Gate.Mode.ShouldBe(TrackingMode.TrackingRole);
        host.Lifecycle.Stopped.ShouldBe([bob]);
        host.Lifecycle.Started.ShouldBe([alice]);
        var periods = await host.PeriodsAsync();
        periods.Single(p => p.UserId == bob).To.ShouldBe(TestTime.Now + TimeSpan.FromSeconds(30));
        var alicePeriod = periods.Single(p => p.UserId == alice);
        alicePeriod.From.ShouldBe(TestTime.Now + TimeSpan.FromSeconds(30));
        alicePeriod.To.ShouldBeNull();
    }

    [Fact]
    public async Task Reload_WhenLessThan30SecondsPass_ShouldNotReload()
    {
        await using var host = new TrackingHost(Fixture);
        await host.SyncAsync(Snapshot());
        await host.StartReloaderAsync();
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));

        host.Time.Advance(TimeSpan.FromSeconds(29));
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        host.Gate.Mode.ShouldBe(TrackingMode.Bootstrap);

        host.Time.Advance(TimeSpan.FromSeconds(1));
        await TrackingHost.WaitUntilAsync(() => host.Gate.Mode == TrackingMode.TrackingRole, "the reload after 30 s ran");
    }

    [Fact]
    public async Task TryGetTrackedUserId_WhenUserIsNotTracked_ShouldReturnNullAndCreateNoRow()
    {
        await using var host = await HostInTrackingRoleModeAsync();
        var usersBefore = await host.InScopeAsync((db, _) => db.Users.CountAsync(TestContext.Current.CancellationToken));

        host.Gate.TryGetTrackedUserId(_carol).ShouldBeNull();
        host.Gate.TryGetTrackedUserId(TestPlatform.User("stranger")).ShouldBeNull();

        (await host.InScopeAsync((db, _) => db.Users.CountAsync(TestContext.Current.CancellationToken))).ShouldBe(usersBefore);
    }

    [Fact]
    public async Task TryGetTrackedUserId_WhenUserIsTracked_ShouldReturnTheirInternalId()
    {
        await using var host = await HostInTrackingRoleModeAsync();

        host.Gate.TryGetTrackedUserId(_alice).ShouldBe(await host.UserIdAsync(_alice));
    }

    [Fact]
    public async Task Startup_WhenOpenPeriodsExistFromBeforeARestart_ShouldKeepTrackedOnesCloseOthersAndStartEveryTrackedUser()
    {
        await using var host = new TrackingHost(Fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));
        var dave = TestPlatform.User("dave");
        var (alice, bob) = await host.InScopeAsync(async (db, ids) =>
        {
            var ct = TestContext.Current.CancellationToken;
            var guildId = await ids.ResolveGuildAsync(TestPlatform.Guild, ct);
            var aliceId = await ids.ResolveUserAsync(_alice, ct);
            var bobId = await ids.ResolveUserAsync(_bob, ct);
            db.TrackingPeriods.Add(new TrackingPeriod { GuildId = guildId, UserId = aliceId, From = TestTime.Now.AddDays(-3) });
            db.TrackingPeriods.Add(new TrackingPeriod { GuildId = guildId, UserId = bobId, From = TestTime.Now.AddDays(-2) });
            await db.SaveChangesAsync(ct);
            return (aliceId, bobId);
        });
        host.Time.Advance(_later);

        await host.SyncAsync(Snapshot(extraMembers: new PlatformMemberBuilder().ForUser(dave).WithRoles(_statistik).Build()));

        var daveId = await host.UserIdAsync(dave);
        var periods = await host.PeriodsAsync();
        var alicePeriod = periods.Where(p => p.UserId == alice).ShouldHaveSingleItem();
        alicePeriod.From.ShouldBe(TestTime.Now.AddDays(-3));
        alicePeriod.To.ShouldBeNull();
        periods.Single(p => p.UserId == bob).To.ShouldBe(TestTime.Now + _later);
        periods.Single(p => p.UserId == daveId).From.ShouldBe(TestTime.Now + _later);
        host.Lifecycle.Started.ShouldBe([alice, daveId], ignoreOrder: true);
        host.Lifecycle.Stopped.ShouldBeEmpty();
    }

    [Fact]
    public async Task Startup_InBootstrapMode_ShouldLogTheModeAsAStructuredWarning()
    {
        await using var host = new TrackingHost(Fixture);

        await host.SyncAsync(Snapshot());

        var record = host.Logs.GetSnapshot().Where(r => r.Message.StartsWith("Bootstrap mode", StringComparison.Ordinal)).ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldContain("until a tracking role is set (temporary, #62)");
        record.StructuredState!.ShouldContain(kv => kv.Key == "Platform" && kv.Value == nameof(PlatformKind.Discord));
        record.StructuredState!.ShouldContain(kv => kv.Key == "{OriginalFormat}");
    }

    [Fact]
    public async Task Startup_WithATrackingRole_ShouldLogTheRoleNameAsStructuredInformation()
    {
        await using var host = new TrackingHost(Fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));

        await host.SyncAsync(Snapshot());

        var record = host.Logs.GetSnapshot().Where(r => r.Message.StartsWith("Tracking role", StringComparison.Ordinal)).ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Information);
        record.StructuredState!.ShouldContain(kv => kv.Key == "RoleName" && kv.Value == "Statistik");
    }

    private static PlatformMemberUpdated Updated(PlatformRef user, params PlatformRef[] roles) =>
        new PlatformMemberUpdatedBuilder()
            .WithMember(new PlatformMemberBuilder().ForUser(user).WithRoles(roles).Build())
            .Build();

    private static PlatformGuildAvailable Snapshot(bool withTrackingRole = true, params PlatformMember[] extraMembers)
    {
        var roles = new List<PlatformRole>
        {
            new PlatformRoleBuilder().WithRef(_admin).Named("Admin").AsAdministrator().Build(),
            new PlatformRoleBuilder().WithRef(_mods).Named("Mods").Build(),
        };
        if (withTrackingRole)
        {
            roles.Add(new PlatformRoleBuilder().WithRef(_statistik).Named("Statistik").Build());
        }

        return new PlatformGuildAvailableBuilder()
            .WithRoles([.. roles])
            .WithMembers(
            [
                new PlatformMemberBuilder().ForUser(_alice).WithRoles(withTrackingRole ? [_statistik] : []).Build(),
                new PlatformMemberBuilder().ForUser(_bob).WithRoles(_admin).Build(),
                new PlatformMemberBuilder().ForUser(_carol).Build(),
                .. extraMembers,
            ])
            .Build();
    }

    private async Task<TrackingHost> HostInTrackingRoleModeAsync()
    {
        var host = new TrackingHost(Fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));
        await host.SyncAsync(Snapshot());
        host.Lifecycle.Clear();
        return host;
    }
}
