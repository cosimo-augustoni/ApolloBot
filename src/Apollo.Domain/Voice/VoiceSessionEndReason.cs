namespace Apollo.Domain.Voice;

/// <summary>
/// Why a voice session ended (<c>voice_sessions.end_reason</c>).
/// </summary>
public enum VoiceSessionEndReason
{
    /// <summary>The user left voice.</summary>
    Left,

    /// <summary>The user moved to another channel; a session in the new channel starts at the same instant.</summary>
    Moved,

    /// <summary>The user lost tracking while in voice.</summary>
    TrackingStopped,

    /// <summary>
    /// The bot did not see how the session ended: it was restarted or disconnected for longer than the reconciliation
    /// allows, or failed to store the change (#98). The end time is the last moment the bot knew the session was going
    /// on, so stats can filter or clip these sessions. Stored as <c>BotRestart</c> before #98.
    /// </summary>
    Unobserved,
}
