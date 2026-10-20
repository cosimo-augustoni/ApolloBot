namespace Apollo.Web.Client.Contracts;

/// <summary>
/// A <c>presence_activity_segments</c> row (scope §5.1, #115): a stretch of a presence session with unchanged rich
/// presence, raw as the platform sent it. Every rich field is <see langword="null"/> when the app sent none.
/// </summary>
/// <param name="ActivityType">As stored: <c>Playing</c>, <c>Streaming</c>, <c>Listening</c>, <c>Watching</c> or <c>Competing</c>.</param>
/// <param name="ActivityName">The activity as the platform reports it, e.g. the game title.</param>
/// <param name="EndedAt">When the segment ended (UTC).</param>
/// <param name="Details">What the user is doing, e.g. the song title.</param>
/// <param name="State">The user's state, e.g. the artists or "In Queue".</param>
/// <param name="LargeText">The large image's hover text, e.g. the album.</param>
/// <param name="SmallText">The small image's hover text.</param>
/// <param name="PartyId">The platform's party ID.</param>
/// <param name="PartySize">The party's size.</param>
/// <param name="PartyMax">The party's maximum size.</param>
/// <param name="ApplicationId">The platform's application ID.</param>
/// <param name="ReportedStart">The start the activity reported (UTC).</param>
/// <param name="ReportedEnd">The end the activity reported (UTC).</param>
public sealed record PresenceSegmentDetailsDto(
    string ActivityType,
    string ActivityName,
    DateTimeOffset EndedAt,
    string? Details = null,
    string? State = null,
    string? LargeText = null,
    string? SmallText = null,
    string? PartyId = null,
    long? PartySize = null,
    long? PartyMax = null,
    string? ApplicationId = null,
    DateTimeOffset? ReportedStart = null,
    DateTimeOffset? ReportedEnd = null);
