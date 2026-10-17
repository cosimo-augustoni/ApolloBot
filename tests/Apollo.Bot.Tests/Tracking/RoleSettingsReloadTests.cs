using System.Diagnostics.Metrics;
using Apollo.Bot.Messages;
using Apollo.Data.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing.Data;
using Apollo.Testing.Diagnostics;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Bot.Tests.Tracking;

/// <summary>
/// A tracking role saved by the admin page (#33) through <see cref="RoleSettingsStore"/> reaches the running bot with
/// the gate's 30 s reload (scope §5.1.2), without a restart.
/// </summary>
public sealed class RoleSettingsReloadTests(TimescaleFixture fixture) : TimescaleTest(fixture), IDisposable
{
    private static readonly PlatformRef _statistik = TestPlatform.Role("statistik");
    private static readonly PlatformRef _mitglied = TestPlatform.Role("mitglied");
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _bob = TestPlatform.User("bob");
    private static readonly PlatformRef _general = TestPlatform.Channel("general");

    private readonly TestMeterFactory _meters = new();

    [Fact]
    public async Task TrackingRoleChangedViaSettings_WhenReloadIntervalElapses_ShouldApplyNewGate()
    {
        await using var host = new TrackingHost(Fixture, services =>
        {
            services.AddSingleton<IMeterFactory>(_meters);
            services.AddMessageRecording();
        });
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));
        await host.SyncAsync(new PlatformGuildAvailableBuilder()
            .WithRoles(
                new PlatformRoleBuilder().WithRef(_statistik).Named("Statistik").Build(),
                new PlatformRoleBuilder().WithRef(_mitglied).Named("Mitglied").Build())
            .WithChannels(new PlatformChannelBuilder().WithRef(_general).Named("general").Build())
            .WithMembers(Member(_alice, _statistik), Member(_bob, _mitglied))
            .Build());
        var (alice, bob) = (await host.UserIdAsync(_alice), await host.UserIdAsync(_bob));
        host.Gate.IsTracked(alice).ShouldBeTrue();
        await host.StartReloaderAsync();
        host.Lifecycle.Clear();

        await host.InScopeAsync(async (db, ids) =>
        {
            var ct = TestContext.Current.CancellationToken;
            var guildId = await ids.ResolveGuildAsync(TestPlatform.Guild, ct);
            var mitglied = await ids.ResolveRoleAsync(TestPlatform.Guild, _mitglied, ct);
            var admin = await ids.ResolveUserAsync(TestPlatform.User("anna"), ct);
            return await new RoleSettingsStore(db, host.Time).SetTrackingRoleAsync(guildId, PlatformKind.Discord, mitglied, admin, ct);
        });
        host.Time.Advance(TimeSpan.FromSeconds(30));
        await TrackingHost.WaitUntilAsync(() => host.Lifecycle.All.Count == 2, "the reload applied the new tracking role");
        await host.RaiseAsync(new PlatformMessageCreatedBuilder().WithMessage(TestPlatform.Message("m1")).ByAuthor(_alice).InChannel(_general).Build());
        await host.RaiseAsync(new PlatformMessageCreatedBuilder().WithMessage(TestPlatform.Message("m2")).ByAuthor(_bob).InChannel(_general).Build());

        host.Lifecycle.Stopped.ShouldBe([alice]);
        host.Lifecycle.Started.ShouldBe([bob]);
        var authors = await host.InScopeAsync((db, _) => db.MessageEvents.AsNoTracking().Select(e => e.UserId).ToListAsync(TestContext.Current.CancellationToken));
        authors.ShouldBe([bob]);
    }

    public void Dispose() => _meters.Dispose();

    private static PlatformMember Member(PlatformRef user, params PlatformRef[] roles) =>
        new PlatformMemberBuilder().ForUser(user).WithRoles(roles).Build();
}
