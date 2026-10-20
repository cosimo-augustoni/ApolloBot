using Apollo.Data.Presence;

namespace Apollo.Testing.Data;

/// <summary>
/// A <see cref="PresenceActivitySession"/> with its segments, as the recorder and the #115 backfill store it.
/// </summary>
public sealed record PresenceSessionRows(PresenceActivitySession Session, IReadOnlyList<PresenceActivitySegment> Segments)
{
    /// <summary>
    /// The session and its segments, for <c>DbContext.AddRange</c>.
    /// </summary>
    public object[] All => [Session, .. Segments];
}
