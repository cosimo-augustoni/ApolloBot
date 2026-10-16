namespace Apollo.Bot.Status;

/// <summary>
/// Temporary default while no voice recorder exists: always 0. The voice recorder (#16) replaces it with its own
/// <see cref="IOpenVoiceSessionCounter"/>; delete this class then.
/// </summary>
internal sealed class NoOpenVoiceSessionCounter : IOpenVoiceSessionCounter
{
    public int CountOpenSessions() => 0;
}
