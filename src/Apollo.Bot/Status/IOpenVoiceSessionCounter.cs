namespace Apollo.Bot.Status;

/// <summary>
/// How many voice sessions the voice recorder holds open, for the heartbeat's <c>open_voice_sessions</c>.
/// </summary>
/// <remarks>
/// Until the voice recorder (#16) registers its own implementation, <see cref="NoOpenVoiceSessionCounter"/> reports 0.
/// </remarks>
public interface IOpenVoiceSessionCounter
{
    int CountOpenSessions();
}
