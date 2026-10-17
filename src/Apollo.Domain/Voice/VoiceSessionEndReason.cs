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
    /// The bot did not see how the session ended: it stopped or crashed, or missed the change while disconnected. The
    /// end time is when the bot noticed, so stats can filter or clip these sessions.
    /// </summary>
    BotRestart,
}
