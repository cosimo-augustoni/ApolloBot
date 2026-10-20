using Apollo.Domain.Presence;

namespace Apollo.Data.Presence;

/// <summary>
/// A stretch of a <see cref="PresenceActivitySession"/> with unchanged rich presence, raw as the platform sends it
/// (scope §5.1, #115). Inserted together with its session once the session closed, so the table is strictly
/// append-only. The segments of a session cover it from start to end without gaps; a segment belongs to the session of
/// the same guild, user, type and name whose span contains it (no foreign key, as for <c>voice_state_segments</c>).
/// Sessions stored before #115 have one segment without rich fields.
/// </summary>
public sealed class PresenceActivitySegment
{
    public long Id { get; init; }

    public Guid GuildId { get; init; }

    /// <summary>
    /// When the segment started; the hypertable's partition column.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    public Guid UserId { get; init; }

    public PresenceActivityType ActivityType { get; init; }

    public required string ActivityName { get; init; }

    /// <summary>
    /// When the segment ended.
    /// </summary>
    public DateTimeOffset EndedAt { get; init; }

    /// <summary>
    /// What the user is doing, e.g. the song title; cut to <see cref="PresenceActivityDetails.MaxTextLength"/>.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// The user's state, e.g. the artists or "In Queue"; cut to <see cref="PresenceActivityDetails.MaxTextLength"/>.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// The large image's hover text, e.g. the album; cut to <see cref="PresenceActivityDetails.MaxTextLength"/>.
    /// </summary>
    public string? LargeText { get; init; }

    /// <summary>
    /// The small image's hover text; cut to <see cref="PresenceActivityDetails.MaxTextLength"/>.
    /// </summary>
    public string? SmallText { get; init; }

    /// <summary>
    /// The platform's party ID, as text; never resolved.
    /// </summary>
    public string? PartyExtId { get; init; }

    public long? PartySize { get; init; }

    public long? PartyMax { get; init; }

    /// <summary>
    /// The platform's application ID, as text; never resolved.
    /// </summary>
    public string? ApplicationExtId { get; init; }

    /// <summary>
    /// The start the activity reported when the segment started (e.g. of the track).
    /// </summary>
    public DateTimeOffset? ReportedStart { get; init; }

    /// <summary>
    /// The end the activity reported when the segment started (e.g. of the track).
    /// </summary>
    public DateTimeOffset? ReportedEnd { get; init; }
}
