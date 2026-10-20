using System.Diagnostics.Metrics;
using Apollo.Bot.Names;
using Apollo.Bot.Tests.Tracking;
using Apollo.Data.Names;
using Apollo.Domain.Names;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Diagnostics;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Diagnostics;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Apollo.Bot.Tests.Names;

public sealed class MemberNameRecorderTests(TimescaleFixture fixture) : TimescaleTest(fixture), IDisposable
{
    private static readonly PlatformRef _statistik = TestPlatform.Role("statistik");
    private static readonly PlatformRef _gamer = TestPlatform.Role("gamer");

    // alice and bob hold the tracking role; carol does not.
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _bob = TestPlatform.User("bob");
    private static readonly PlatformRef _carol = TestPlatform.User("carol");

    private static readonly TimeSpan _later = TimeSpan.FromHours(1);

    private readonly TestMeterFactory _meters = new();

    public enum HandlerOrder
    {
        AfterGuildStateSync,
        BeforeGuildStateSync,
    }

    [Fact]
    public async Task Update_WhenTrackedMemberChangesTheirNickname_ShouldStoreOneChangedRowWithTheNewNickname()
    {
        await using var host = await HostAsync();
        host.Time.Advance(_later);

        await UpdateAsync(host, Alice().WithNickname("Ali"));

        var (guildId, aliceId) = (await GuildIdAsync(host), await host.UserIdAsync(_alice));
        var rows = await RowsAsync(host, _alice);
        rows.Select(Row).ShouldBe([
            (MemberNameChangeKind.Baseline, null, "Alice", "alice", TestTime.Now),
            (MemberNameChangeKind.Changed, "Ali", "Alice", "alice", TestTime.Now + _later),
        ]);
        rows.ShouldAllBe(r => r.GuildId == guildId && r.UserId == aliceId);
    }

    [Theory]
    [InlineData("Alicia", "alice")]
    [InlineData("Alice", "alice_2")]
    [InlineData(null, "alice")]
    public async Task Update_WhenTheGlobalNameOrUsernameChanges_ShouldStoreOneChangedRow(string? globalName, string username)
    {
        await using var host = await HostAsync();
        host.Time.Advance(_later);

        await UpdateAsync(host, Alice().WithGlobalName(globalName).WithUsername(username));

        (await RowsAsync(host, _alice)).Select(Row).ShouldBe([
            (MemberNameChangeKind.Baseline, null, "Alice", "alice", TestTime.Now),
            (MemberNameChangeKind.Changed, null, globalName, username, TestTime.Now + _later),
        ]);
    }

    [Fact]
    public async Task Update_WhenOnlyTheRolesChange_ShouldStoreNothing()
    {
        await using var host = await HostAsync();
        host.Time.Advance(_later);

        await UpdateAsync(host, Alice().WithRoles(_statistik, _gamer));

        (await RowsAsync(host, _alice)).Select(r => r.Kind).ShouldBe([MemberNameChangeKind.Baseline]);
    }

    [Fact]
    public async Task Update_WhenTheNicknameIsRemoved_ShouldStoreAChangedRowWithoutNickname()
    {
        await using var host = await HostAsync(Alice().WithNickname("Ali"));
        host.Time.Advance(_later);

        await UpdateAsync(host, Alice().WithNickname(null));

        (await RowsAsync(host, _alice)).Select(Row).ShouldBe([
            (MemberNameChangeKind.Baseline, "Ali", "Alice", "alice", TestTime.Now),
            (MemberNameChangeKind.Changed, null, "Alice", "alice", TestTime.Now + _later),
        ]);
    }

    [Fact]
    public async Task Update_WhenTheMemberIsUntracked_ShouldStoreNothingAndCountItAsDropped()
    {
        await using var host = await HostAsync();
        using var dropped = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.DroppedCounter);

        await UpdateAsync(host, Carol().WithNickname("Caro"));

        (await AllRowsAsync(host)).ShouldNotContain(r => r.Username == "carol");
        dropped.GetMeasurementSnapshot().Select(m => m.Tags["reason"]).ShouldBe(["untracked"]);
    }

    [Fact]
    public async Task Update_WhenStored_ShouldCountItAsRecorded()
    {
        await using var host = await HostAsync();
        using var recorded = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.RecordedCounter);
        host.Time.Advance(_later);

        await UpdateAsync(host, Alice().WithNickname("Ali"));
        await UpdateAsync(host, Alice().WithNickname("Ali").WithRoles(_statistik, _gamer));

        recorded.GetMeasurementSnapshot().Select(m => m.Tags["event_type"]).ShouldBe([nameof(PlatformMemberUpdated)]);
    }

    [Fact]
    public async Task Update_WhenTheSameUpdateIsDeliveredTwice_ShouldStoreOneRow()
    {
        await using var host = await HostAsync();
        host.Time.Advance(_later);
        await UpdateAsync(host, Alice().WithNickname("Ali"));
        host.Time.Advance(_later);

        // A replay carries no timestamp of its own, so it is stamped on receipt again.
        await UpdateAsync(host, Alice().WithNickname("Ali"));

        (await RowsAsync(host, _alice)).Select(r => r.Kind).ShouldBe([MemberNameChangeKind.Baseline, MemberNameChangeKind.Changed]);
    }

    [Theory]
    [InlineData(HandlerOrder.AfterGuildStateSync)]
    [InlineData(HandlerOrder.BeforeGuildStateSync)]
    public async Task TrackingStarted_WhenAMemberGainsTheTrackingRole_ShouldStoreOneBaselineWithTheirCurrentNames(HandlerOrder order)
    {
        await using var host = await HostAsync(order: order);
        host.Time.Advance(_later);

        await UpdateAsync(host, Carol().WithNickname("Caro").WithRoles(_statistik));

        (await RowsAsync(host, _carol)).Select(Row).ShouldBe([
            (MemberNameChangeKind.Baseline, "Caro", "Carol", "carol", TestTime.Now + _later),
        ]);
    }

    [Fact]
    public async Task TrackingStarted_WhenTrackingStartsAgainAfterAGap_ShouldStoreANewBaselineWithTheSameNames()
    {
        await using var host = await HostAsync();
        host.Time.Advance(_later);
        await UpdateAsync(host, Alice().WithRoles());
        host.Time.Advance(_later);

        await UpdateAsync(host, Alice());

        (await RowsAsync(host, _alice)).Select(Row).ShouldBe([
            (MemberNameChangeKind.Baseline, null, "Alice", "alice", TestTime.Now),
            (MemberNameChangeKind.Baseline, null, "Alice", "alice", TestTime.Now + (2 * _later)),
        ]);
    }

    [Fact]
    public async Task Sync_WhenMembersWereTrackedBeforeNamesWereRecorded_ShouldStoreTheirBaselineOnTheFirstSync()
    {
        // The bot before #114: tracking periods, but no names.
        await using (var before = await HostAsync(recordNames: false))
        {
            (await AllRowsAsync(before)).ShouldBeEmpty();
        }

        await using var host = await HostAsync(startAfter: _later);

        (await AllRowsAsync(host)).Select(Row).ShouldBe([
            (MemberNameChangeKind.Baseline, null, "Alice", "alice", TestTime.Now + _later),
            (MemberNameChangeKind.Baseline, null, "Bob", "bob", TestTime.Now + _later),
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task Sync_WhenTheNicknameChangedWhileTheBotWasDown_ShouldStoreAChangedRowAtTheSyncAfterTheStart()
    {
        await using (await HostAsync())
        {
        }

        await using var host = await HostAsync(Alice().WithNickname("Ali"), startAfter: 2 * _later);

        (await RowsAsync(host, _alice)).Select(Row).ShouldBe([
            (MemberNameChangeKind.Baseline, null, "Alice", "alice", TestTime.Now),
            (MemberNameChangeKind.Changed, "Ali", "Alice", "alice", TestTime.Now + (2 * _later)),
        ]);
    }

    [Fact]
    public async Task Sync_WhenTheNicknameChangedWhileTheGatewayWasDisconnected_ShouldStoreAChangedRowAtTheReconnectSync()
    {
        await using var host = await HostAsync();
        host.Time.Advance(_later);

        await SyncAsync(host, Alice().WithNickname("Ali"));

        (await RowsAsync(host, _alice)).Select(Row).ShouldBe([
            (MemberNameChangeKind.Baseline, null, "Alice", "alice", TestTime.Now),
            (MemberNameChangeKind.Changed, "Ali", "Alice", "alice", TestTime.Now + _later),
        ]);
        (await RowsAsync(host, _bob)).Select(r => r.Kind).ShouldBe([MemberNameChangeKind.Baseline]);
    }

    [Fact]
    public async Task Update_WhenItWaitedWhileASyncStoredNewerNames_ShouldNotUndoThem()
    {
        await using var host = await HostAsync();
        var receivedAt = TestTime.Now + _later;
        host.Time.Advance(2 * _later);
        await SyncAsync(host, Alice().WithNickname("Al"));

        // Received before the sync, handled after it: the sync read the platform's newer state.
        await host.RaiseAsync(Updated(Alice().WithNickname("Ali")).At(receivedAt).Build());

        (await RowsAsync(host, _alice)).Select(Row).ShouldBe([
            (MemberNameChangeKind.Baseline, null, "Alice", "alice", TestTime.Now),
            (MemberNameChangeKind.Changed, "Al", "Alice", "alice", TestTime.Now + (2 * _later)),
        ]);
    }

    [Fact]
    public async Task Update_WhenItWaitedWhileTrackingStarted_ShouldStoreNoRowBeforeTheBaseline()
    {
        await using var host = await HostAsync();
        var receivedAt = TestTime.Now + _later;
        host.Time.Advance(2 * _later);
        await UpdateAsync(host, Carol().WithNickname("Caro").WithRoles(_statistik));

        // An older update of carol, received before her tracking started and handled after the baseline.
        await host.RaiseAsync(Updated(Carol().WithNickname("C").WithRoles(_statistik)).At(receivedAt).Build());

        (await RowsAsync(host, _carol)).Select(Row).ShouldBe([
            (MemberNameChangeKind.Baseline, "Caro", "Carol", "carol", TestTime.Now + (2 * _later)),
        ]);
    }

    public void Dispose() => _meters.Dispose();

    private static (MemberNameChangeKind Kind, string? Nickname, string? GlobalName, string Username, DateTimeOffset OccurredAt) Row(MemberNameChange row) =>
        (row.Kind, row.Nickname, row.GlobalName, row.Username, row.OccurredAt);

    private static PlatformMemberBuilder Alice() =>
        new PlatformMemberBuilder().ForUser(_alice).WithGlobalName("Alice").WithUsername("alice").WithRoles(_statistik);

    private static PlatformMemberBuilder Bob() =>
        new PlatformMemberBuilder().ForUser(_bob).WithGlobalName("Bob").WithUsername("bob").WithRoles(_statistik);

    private static PlatformMemberBuilder Carol() =>
        new PlatformMemberBuilder().ForUser(_carol).WithGlobalName("Carol").WithUsername("carol");

    private static PlatformMemberUpdatedBuilder Updated(PlatformMemberBuilder member) =>
        new PlatformMemberUpdatedBuilder().WithMember(member.Build());

    /// <summary>
    /// The member changes on the platform (its cache, which the sync and the tracking start read) and the update is
    /// raised, stamped on receipt at the host's current time.
    /// </summary>
    private static Task UpdateAsync(TrackingHost host, PlatformMemberBuilder member)
    {
        host.Platform.WithMember(member.Build());
        return host.RaiseAsync(Updated(member).At(host.Time.GetUtcNow()).Build());
    }

    /// <summary>
    /// A full guild sync, as on startup and after every reconnect, with alice as given.
    /// </summary>
    private static Task SyncAsync(TrackingHost host, PlatformMemberBuilder? alice = null) =>
        host.SyncAsync(new PlatformGuildAvailableBuilder()
            .WithRoles(
                new PlatformRoleBuilder().WithRef(_statistik).Named("Statistik").Build(),
                new PlatformRoleBuilder().WithRef(_gamer).Named("Gamer").Build())
            .WithMembers((alice ?? Alice()).Build(), Bob().Build(), Carol().Build())
            .At(host.Time.GetUtcNow())
            .Build());

    private static Task<List<MemberNameChange>> AllRowsAsync(TrackingHost host) =>
        host.InScopeAsync((db, _) => db.MemberNameChanges.AsNoTracking()
            .OrderBy(r => r.OccurredAt).ThenBy(r => r.Id)
            .ToListAsync(TestContext.Current.CancellationToken));

    private static async Task<List<MemberNameChange>> RowsAsync(TrackingHost host, PlatformRef user)
    {
        var userId = await host.UserIdAsync(user);
        return [.. (await AllRowsAsync(host)).Where(r => r.UserId == userId)];
    }

    private static Task<Guid> GuildIdAsync(TrackingHost host) =>
        host.InScopeAsync(async (_, ids) => (await ids.TryGetGuildAsync(TestPlatform.Guild, TestContext.Current.CancellationToken)).GetValueOrDefault());

    /// <summary>
    /// Registers the recorder's member-update handler before <c>GuildStateSync</c>'s, so it runs before the gate learns
    /// of a role change (the dispatcher runs handlers in registration order).
    /// </summary>
    private static void MoveRecorderFirst(IServiceCollection services)
    {
        var recorder = services.Single(d =>
            d.ServiceType == typeof(IPlatformEventHandler<PlatformMemberUpdated>) && d.ImplementationType == typeof(MemberNameRecorder));
        services.Remove(recorder);
        services.Insert(0, recorder);
    }

    /// <summary>
    /// The bot after startup: the first full guild sync, <paramref name="startAfter"/> after <see cref="TestTime.Now"/>.
    /// </summary>
    private async Task<TrackingHost> HostAsync(
        PlatformMemberBuilder? alice = null,
        HandlerOrder order = HandlerOrder.AfterGuildStateSync,
        TimeSpan? startAfter = null,
        bool recordNames = true)
    {
        var host = new TrackingHost(Fixture, services =>
        {
            services.AddSingleton<IMeterFactory>(_meters);
            if (recordNames)
            {
                services.AddNameRecording();
            }

            if (order == HandlerOrder.BeforeGuildStateSync)
            {
                MoveRecorderFirst(services);
            }
        });
        host.Time.Advance(startAfter ?? TimeSpan.Zero);
        if (!await host.InScopeAsync((db, _) => db.RoleSettings.AnyAsync(TestContext.Current.CancellationToken)))
        {
            await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));
        }

        await SyncAsync(host, alice);
        return host;
    }
}
