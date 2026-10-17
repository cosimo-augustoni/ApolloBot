namespace Apollo.Web.Client.Contracts;

/// <summary>A <c>presence_activity_sessions</c> row (scope §5.1, #19).</summary>
/// <param name="ActivityType">As stored: <c>Playing</c>, <c>Streaming</c>, <c>Listening</c>, <c>Watching</c> or <c>Competing</c>.</param>
/// <param name="ActivityName">The activity as the platform reports it, e.g. the game title.</param>
/// <param name="EndedAt">When the session ended (UTC).</param>
public sealed record PresenceActivityDetailsDto(string ActivityType, string ActivityName, DateTimeOffset EndedAt);
