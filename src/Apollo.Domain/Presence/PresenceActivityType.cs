namespace Apollo.Domain.Presence;

/// <summary>
/// The kind of a recorded presence activity (<c>presence_activity_sessions.activity_type</c>). Custom statuses are not
/// activities and never get a session (#19).
/// </summary>
public enum PresenceActivityType
{
    Playing,
    Streaming,
    Listening,
    Watching,
    Competing,
}
