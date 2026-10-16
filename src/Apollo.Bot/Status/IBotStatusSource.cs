namespace Apollo.Bot.Status;

/// <summary>
/// Gathers the bot's current status from the platform adapter, the event dispatcher and the voice recorder.
/// </summary>
public interface IBotStatusSource
{
    BotStatusSnapshot GetCurrent();
}
