namespace Apollo.Domain.Presence;

/// <summary>
/// The rich presence of one activity, raw as the platform sends it (<c>presence_activity_segments</c>, #115): free text
/// the app chooses (for Spotify the song title, the artists and the album; for a game its mode, map or rank), the party
/// and the activity's own timestamps. Every field is optional; <see cref="None"/> is an activity that sends none.
/// </summary>
/// <param name="Details">What the user is doing, e.g. the song title or "Competitive".</param>
/// <param name="State">The user's current state, e.g. the artists or "In Queue".</param>
/// <param name="LargeText">The hover text of the large image, e.g. the album.</param>
/// <param name="SmallText">The hover text of the small image, e.g. a rank.</param>
/// <param name="PartyId">The platform's party ID, as text.</param>
/// <param name="PartySize">The party's current size.</param>
/// <param name="PartyMax">The party's maximum size.</param>
/// <param name="ApplicationId">The platform's application ID, as text.</param>
/// <param name="ReportedStart">The start the activity reports, e.g. of the current track.</param>
/// <param name="ReportedEnd">The end the activity reports, e.g. of the current track.</param>
public sealed record PresenceActivityDetails(
    string? Details = null,
    string? State = null,
    string? LargeText = null,
    string? SmallText = null,
    string? PartyId = null,
    long? PartySize = null,
    long? PartyMax = null,
    string? ApplicationId = null,
    DateTimeOffset? ReportedStart = null,
    DateTimeOffset? ReportedEnd = null)
{
    /// <summary>
    /// The longest text that is stored: Discord's limit for <see cref="Details"/> and <see cref="State"/>.
    /// </summary>
    public const int MaxTextLength = 128;

    public static PresenceActivityDetails None { get; } = new();

    /// <summary>
    /// Whether <paramref name="other"/> continues this segment: every field except the application and the reported
    /// timestamps is the same, so seeking in a song does not split it.
    /// </summary>
    public bool IsSameSegmentAs(PresenceActivityDetails other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return this with { ApplicationId = null, ReportedStart = null, ReportedEnd = null }
            == other with { ApplicationId = null, ReportedStart = null, ReportedEnd = null };
    }

    /// <summary>
    /// <paramref name="text"/> cut to <see cref="MaxTextLength"/>, never between the two halves of a surrogate pair.
    /// </summary>
    public static string? Cut(string? text)
    {
        if (text is null || text.Length <= MaxTextLength)
        {
            return text;
        }

        return text[..(char.IsHighSurrogate(text[MaxTextLength - 1]) ? MaxTextLength - 1 : MaxTextLength)];
    }
}
