using System.Globalization;
using Apollo.Data.Presence;
using Apollo.Domain.Presence;

namespace Apollo.Testing.Data;

/// <summary>
/// Builds a <see cref="PresenceActivitySession"/> row: by default one hour of <see cref="PresenceActivityType.Playing"/>
/// "Factorio" from <see cref="TestTime.Now"/>, with its covering segment from <see cref="BuildRows"/>. Guild and user are
/// internal IDs; seed them first.
/// </summary>
public sealed class PresenceActivitySessionBuilder : TestDataBuilder<PresenceActivitySessionBuilder, PresenceActivitySession>
{
    public PresenceActivitySessionBuilder In(Guid guildId) => With(s => Copy(s, guildId: guildId));

    public PresenceActivitySessionBuilder By(Guid userId) => With(s => Copy(s, userId: userId));

    public PresenceActivitySessionBuilder Playing(string name) => Doing(PresenceActivityType.Playing, name);

    public PresenceActivitySessionBuilder Doing(PresenceActivityType type, string name) =>
        With(s => Copy(s, activityType: type, activityName: name));

    public PresenceActivitySessionBuilder From(DateTimeOffset startedAt) => With(s => Copy(s, startedAt: startedAt));

    public PresenceActivitySessionBuilder From(string startedAtUtc) => From(Parse(startedAtUtc));

    public PresenceActivitySessionBuilder Until(DateTimeOffset endedAt) => With(s => Copy(s, endedAt: endedAt));

    public PresenceActivitySessionBuilder Until(string endedAtUtc) => Until(Parse(endedAtUtc));

    /// <summary>
    /// The session with its segments, as production stores it (#115): one segment covering it without rich fields.
    /// Seed sessions with this rather than <see cref="TestDataBuilder{TBuilder,T}.Build"/>.
    /// </summary>
    public PresenceSessionRows BuildRows()
    {
        var session = Build();
        return new PresenceSessionRows(session, [CoveringSegment(session)]);
    }

    /// <summary>
    /// The segment without rich fields that covers <paramref name="session"/>, as for sessions without rich presence.
    /// </summary>
    public static PresenceActivitySegment CoveringSegment(PresenceActivitySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new PresenceActivitySegment
        {
            GuildId = session.GuildId,
            OccurredAt = session.OccurredAt,
            UserId = session.UserId,
            ActivityType = session.ActivityType,
            ActivityName = session.ActivityName,
            EndedAt = session.EndedAt,
        };
    }

    protected override PresenceActivitySession CreateDefault() => new()
    {
        OccurredAt = TestTime.Now,
        EndedAt = TestTime.Now.AddHours(1),
        ActivityType = PresenceActivityType.Playing,
        ActivityName = "Factorio",
    };

    private static DateTimeOffset Parse(string utc) => DateTimeOffset.Parse(utc, CultureInfo.InvariantCulture);

    private static PresenceActivitySession Copy(
        PresenceActivitySession s,
        Guid? guildId = null,
        Guid? userId = null,
        PresenceActivityType? activityType = null,
        string? activityName = null,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? endedAt = null) => new()
        {
            GuildId = guildId ?? s.GuildId,
            UserId = userId ?? s.UserId,
            ActivityType = activityType ?? s.ActivityType,
            ActivityName = activityName ?? s.ActivityName,
            OccurredAt = startedAt ?? s.OccurredAt,
            EndedAt = endedAt ?? s.EndedAt,
        };
}
