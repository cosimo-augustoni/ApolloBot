namespace Apollo.Bot.Status;

/// <summary>
/// How many voice sessions the voice recorder holds open, for the heartbeat's <c>open_voice_sessions</c>.
/// </summary>
/// <remarks>
/// Implemented by the voice recorder's in-memory store (#16), registered with
/// <see cref="Voice.VoiceRecordingServiceCollectionExtensions.AddVoiceRecording"/>.
/// </remarks>
public interface IOpenVoiceSessionCounter
{
    int CountOpenSessions();
}
