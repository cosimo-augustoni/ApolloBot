using System.Globalization;
using Apollo.Data;
using Apollo.Data.Membership;
using Apollo.Data.Messages;
using Apollo.Data.Status;
using Apollo.Data.Tracking;
using Apollo.Data.Voice;
using Apollo.Stats.Tests.Raw;
using Apollo.Stats.Sanity;
using Apollo.Testing;
using Apollo.Testing.Data;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Stats.Tests.Sanity;

/// <summary>
/// The guild of <see cref="RawStatsSeed"/> (range 10 to 14 January 2026) plus the rows only the sanity checks read:
/// heartbeats, tracking periods, mentions, voice segments and membership events. "Now" is <see cref="TestTime.Now"/>
/// (15 January, 09:00 UTC).
/// </summary>
internal sealed record SanitySeed(RawStatsSeed Raw)
{
    public Guid GuildId => Raw.GuildId;

    public Guid Alice => Raw.Alice;

    public Guid Bob => Raw.Bob;

    public Guid General => Raw.General;

    public Guid Lounge => Raw.Lounge;

    public DataSanityQuery Query() => new(GuildId, RawStatsSeed.Jan10, RawStatsSeed.Jan14);

    public static async Task<SanitySeed> CreateAsync(ApolloDbContext db, CancellationToken cancellationToken) =>
        new(await RawStatsSeed.CreateAsync(db, cancellationToken));

    public static DataSanityService Service(ApolloDbContext db, FakeTimeProvider? time = null) => new(db, time ?? TestTime.CreateProvider());

    public static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    public TrackingPeriod Period(Guid userId, string fromUtc, string? toUtc) =>
        new() { GuildId = GuildId, UserId = userId, From = Utc(fromUtc), To = toUtc is null ? null : Utc(toUtc) };

    /// <summary>Alice and Bob tracked from before the range on, still open.</summary>
    public TrackingPeriod[] EveryoneTracked() =>
    [
        Period(Alice, "2026-01-01T00:00:00Z", null),
        Period(Bob, "2026-01-01T00:00:00Z", null),
    ];

    public BotStatus Heartbeat(DateTimeOffset at) => new() { GuildId = GuildId, OccurredAt = at, Connected = true };

    /// <summary>One heartbeat every 30 s from <paramref name="from"/> (included) to <paramref name="until"/> (excluded).</summary>
    public IEnumerable<BotStatus> Heartbeats(DateTimeOffset from, DateTimeOffset until)
    {
        for (var at = from; at < until; at += TimeSpan.FromSeconds(30))
        {
            yield return Heartbeat(at);
        }
    }

    public VoiceSessionBuilder Voice(Guid userId, string fromUtc, string? untilUtc) => Raw.Voice(userId, Lounge, fromUtc, untilUtc);

    public MessageEventBuilder Message(Guid userId, string atUtc) => new MessageEventBuilder().In(GuildId, General).By(userId).At(atUtc);

    public ReactionEventBuilder Reaction(Guid userId, string atUtc) => new ReactionEventBuilder().In(GuildId, General).By(userId).At(atUtc);

    public PresenceActivitySessionBuilder Presence(Guid userId, string fromUtc, string untilUtc) =>
        new PresenceActivitySessionBuilder().In(GuildId).By(userId).Playing("Factorio").From(fromUtc).Until(untilUtc);

    public MessageMention Mention(Guid userId, Guid? mentionedUserId, string atUtc) => new()
    {
        GuildId = GuildId,
        ChannelId = General,
        OccurredAt = Utc(atUtc),
        MessageExtId = Guid.NewGuid().ToString("N"),
        UserId = userId,
        MentionedUserId = mentionedUserId,
    };

    public VoiceStateSegment Segment(Guid userId, string atUtc) => new()
    {
        GuildId = GuildId,
        UserId = userId,
        SessionId = 1,
        OccurredAt = Utc(atUtc),
        EndedAt = Utc(atUtc).AddMinutes(30),
    };

    public MembershipEvent Membership(Guid userId, MembershipEventKind kind, string atUtc) =>
        new() { GuildId = GuildId, UserId = userId, OccurredAt = Utc(atUtc), Kind = kind };
}
