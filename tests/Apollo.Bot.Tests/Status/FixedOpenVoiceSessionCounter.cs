using Apollo.Bot.Status;

namespace Apollo.Bot.Tests.Status;

/// <summary>
/// Stands in for the voice recorder's counter where a test registers <c>AddBotStatus</c> without voice recording.
/// </summary>
internal sealed class FixedOpenVoiceSessionCounter(int count) : IOpenVoiceSessionCounter
{
    public int CountOpenSessions() => count;
}
