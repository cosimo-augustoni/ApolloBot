using System.Diagnostics.Metrics;
using Apollo.Bot.Tests.Tracking;
using Apollo.Bot.Tracking;
using Apollo.Bot.Voice;
using Apollo.Data.Status;
using Apollo.Data.Voice;
using Apollo.Domain.Voice;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Apollo.Bot.Tests.Voice;

/// <summary>
/// The voice recorder as the bot wires it, on a <see cref="TrackingHost"/>, with the guild and members the voice tests
/// share: alice and bob hold the tracking role <see cref="Statistik"/>, carol does not.
/// </summary>
internal static class VoiceTestHost
{
    public static readonly PlatformRef Statistik = TestPlatform.Role("statistik");
    public static readonly PlatformRef Alice = TestPlatform.User("alice");
    public static readonly PlatformRef Bob = TestPlatform.User("bob");
    public static readonly PlatformRef Carol = TestPlatform.User("carol");
    public static readonly PlatformRef Lounge = TestPlatform.Channel("lounge");
    public static readonly PlatformRef Gaming = TestPlatform.Channel("gaming");

    /// <summary>
    /// The default reconciliation interval and blind spot (<see cref="VoiceReconciliationOptions"/>).
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxGap = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A host with the voice recorder. Without <paramref name="interval"/> the periodic run is due only after a day, so
    /// tests of other behaviour can advance the fake clock freely. With
    /// <paramref name="hold"/> the gate's tracking start hook for one user waits before it reaches the recorder.
    /// </summary>
    public static TrackingHost Create(
        TimescaleFixture fixture,
        IMeterFactory meters,
        TimeSpan? interval = null,
        IInterceptor? interceptor = null,
        Action<IServiceCollection>? configure = null,
        HeldTrackingStart? hold = null) => new(
        fixture,
        services =>
        {
            services.AddSingleton(meters);
            if (hold is not null)
            {
                services.AddSingleton(hold);
                services.AddTrackingLifecycleHandler<HeldTrackingStart.Handler>();
            }

            services.AddVoiceRecording();
            services.Configure<VoiceReconciliationOptions>(options => options.Interval = interval ?? TimeSpan.FromDays(1));
            configure?.Invoke(services);
        },
        interceptor);

    /// <summary>
    /// The bot's startup: the voice lifecycle service starts (take-over, periodic timer), then the first full guild sync.
    /// </summary>
    public static async Task StartAndSyncAsync(TrackingHost host)
    {
        await StartAsync(host);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        await SyncAsync(host);
    }

    public static Task StartAsync(TrackingHost host) => Lifecycle(host).StartAsync(TestContext.Current.CancellationToken);

    public static IHostedService Lifecycle(TrackingHost host) =>
        host.Services.GetServices<IHostedService>().OfType<VoiceSessionLifecycleService>().Single();

    public static VoiceSessionStore Store(TrackingHost host) => host.Services.GetRequiredService<VoiceSessionStore>();

    /// <summary>
    /// A full guild sync, as on startup and after every reconnect.
    /// </summary>
    public static Task SyncAsync(TrackingHost host) =>
        host.SyncAsync(new PlatformGuildAvailableBuilder()
            .WithRoles(new PlatformRoleBuilder().WithRef(Statistik).Named("Statistik").Build())
            .WithChannels(
                new PlatformChannelBuilder().WithRef(Lounge).Named("Lounge").OfType(PlatformChannelType.Voice).Build(),
                new PlatformChannelBuilder().WithRef(Gaming).Named("Gaming").OfType(PlatformChannelType.Voice).Build())
            .WithMembers(Member(Alice, Statistik), Member(Bob, Statistik), Member(Carol))
            .Build());

    public static PlatformVoiceStateBuilder VoiceOf(PlatformRef user, PlatformRef? channel) =>
        new PlatformVoiceStateBuilder().ForUser(user).InChannel(channel);

    public static PlatformMember Member(PlatformRef user, params PlatformRef[] roles) =>
        new PlatformMemberBuilder().ForUser(user).WithRoles(roles).Build();

    /// <summary>
    /// Raises a voice state update at the host's current time, as the adapter stamps it on receipt.
    /// </summary>
    public static Task ChangeAsync(TrackingHost host, PlatformVoiceStateBuilder state) =>
        host.RaiseAsync(new PlatformVoiceStateChangedBuilder().WithState(state.Build()).At(host.Time.GetUtcNow()).Build());

    /// <summary>
    /// The member's voice state changes on the platform (its cache, which the reconciliation reads) and the update is
    /// raised, as the gateway delivers it.
    /// </summary>
    public static Task ChangeOnPlatformAsync(TrackingHost host, PlatformVoiceStateBuilder state)
    {
        host.Platform.WithVoiceState(state.Build());
        return ChangeAsync(host, state);
    }

    public static Task ConnectionAsync(TrackingHost host, PlatformConnectionState state) =>
        host.RaiseAsync(new PlatformConnectionStateChangedBuilder().WithState(state).At(host.Time.GetUtcNow()).Build());

    /// <summary>
    /// Advances the fake clock by the reconciliation interval and waits until that periodic run finished.
    /// </summary>
    public static async Task RunPeriodicallyAsync(TrackingHost host)
    {
        var store = Store(host);
        var before = store.LastReconciledAt;
        host.Time.Advance(Interval);
        await TrackingHost.WaitUntilAsync(() => store.LastReconciledAt != before, "the periodic voice reconciliation ran");
    }

    /// <summary>
    /// A session and its segment as a previous run left them open.
    /// </summary>
    public static Task<VoiceSession> SeedOpenSessionAsync(
        TrackingHost host,
        PlatformRef user,
        PlatformRef channel,
        DateTimeOffset startedAt,
        bool selfMute = false) =>
        host.InScopeAsync(async (db, ids) =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var session = new VoiceSession
            {
                GuildId = await ids.ResolveGuildAsync(TestPlatform.Guild, cancellationToken),
                OccurredAt = startedAt,
                UserId = await ids.ResolveUserAsync(user, cancellationToken),
                ChannelId = await ids.ResolveChannelAsync(TestPlatform.Guild, channel, cancellationToken),
            };
            db.VoiceSessions.Add(session);
            await db.SaveChangesAsync(cancellationToken);
            db.VoiceStateSegments.Add(new VoiceStateSegment
            {
                GuildId = session.GuildId,
                OccurredAt = startedAt,
                SessionId = session.Id,
                UserId = session.UserId,
                SelfMute = selfMute,
            });
            await db.SaveChangesAsync(cancellationToken);
            return session;
        });

    /// <summary>
    /// A heartbeat of a previous run (<c>bot_status</c>).
    /// </summary>
    public static Task SeedHeartbeatAsync(TrackingHost host, DateTimeOffset at, bool connected = true) =>
        host.InScopeAsync(async (db, ids) =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            db.BotStatuses.Add(new BotStatus
            {
                GuildId = await ids.ResolveGuildAsync(TestPlatform.Guild, cancellationToken),
                OccurredAt = at,
                Connected = connected,
            });
            return await db.SaveChangesAsync(cancellationToken);
        });

    public static Task<List<VoiceSession>> SessionsAsync(TrackingHost host) =>
        host.InScopeAsync((db, _) => db.VoiceSessions.AsNoTracking().OrderBy(s => s.Id).ToListAsync(TestContext.Current.CancellationToken));

    public static Task<List<VoiceStateSegment>> SegmentsAsync(TrackingHost host) =>
        host.InScopeAsync((db, _) => db.VoiceStateSegments.AsNoTracking().OrderBy(s => s.Id).ToListAsync(TestContext.Current.CancellationToken));

    public static Task<Guid> GuildIdAsync(TrackingHost host) =>
        host.InScopeAsync(async (_, ids) => (await ids.TryGetGuildAsync(TestPlatform.Guild, TestContext.Current.CancellationToken)).GetValueOrDefault());

    public static Task<Guid> ChannelIdAsync(TrackingHost host, PlatformRef channel) =>
        host.InScopeAsync(async (_, ids) => (await ids.TryGetChannelAsync(channel, TestContext.Current.CancellationToken)).GetValueOrDefault());

    /// <summary>
    /// The (end, reason) of every session, in order, to compare in one assertion.
    /// </summary>
    public static async Task<List<(DateTimeOffset? EndedAt, VoiceSessionEndReason? EndReason)>> EndsAsync(TrackingHost host) =>
        [.. (await SessionsAsync(host)).Select(s => (s.EndedAt, s.EndReason))];
}
