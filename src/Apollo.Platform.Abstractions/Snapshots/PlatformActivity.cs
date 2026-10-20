namespace Apollo.Platform.Abstractions.Snapshots;

/// <summary>
/// Something a member is doing, as shown in their presence (Discord only, see
/// <see cref="PlatformCapabilities.PresenceActivities"/>), with its rich presence as the platform sends it (#115). Every
/// rich field is optional and may be any text the app chooses.
/// </summary>
/// <param name="Type">The activity kind.</param>
/// <param name="Name">The activity name, e.g. the game title.</param>
/// <param name="ReportedStart">When the activity started, if the platform reports it (for Spotify: the track's start).</param>
public sealed record PlatformActivity(PlatformActivityType Type, string Name, DateTimeOffset? ReportedStart)
{
    /// <summary>
    /// When the activity ends, if the platform reports it (for Spotify: the track's end).
    /// </summary>
    public DateTimeOffset? ReportedEnd { get; init; }

    /// <summary>
    /// What the user is doing, e.g. the song title or the game mode.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// The user's current state, e.g. the artists or "In Queue".
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// The hover text of the large image, e.g. the album.
    /// </summary>
    public string? LargeText { get; init; }

    /// <summary>
    /// The hover text of the small image.
    /// </summary>
    public string? SmallText { get; init; }

    /// <summary>
    /// The platform's party ID: an opaque value of the app, not a reference to anything Apollo records.
    /// </summary>
    public string? PartyId { get; init; }

    /// <summary>
    /// The party's current size.
    /// </summary>
    public long? PartySize { get; init; }

    /// <summary>
    /// The party's maximum size.
    /// </summary>
    public long? PartyMax { get; init; }

    /// <summary>
    /// The platform's ID of the application behind the activity, as text: an opaque value, not a reference.
    /// </summary>
    public string? ApplicationId { get; init; }
}
