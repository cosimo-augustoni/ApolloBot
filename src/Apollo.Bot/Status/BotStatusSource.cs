using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Dispatch;

namespace Apollo.Bot.Status;

internal sealed class BotStatusSource(
    IPlatformConnectionStatus connection,
    IPlatformEventActivity activity,
    IOpenVoiceSessionCounter voiceSessions) : IBotStatusSource
{
    public BotStatusSnapshot GetCurrent()
    {
        var current = connection.Current;
        return new BotStatusSnapshot(
            connection.Guild,
            current.IsConnected,
            current.IsConnected ? current.Latency : null,
            activity.LastEventAt,
            voiceSessions.CountOpenSessions());
    }
}
