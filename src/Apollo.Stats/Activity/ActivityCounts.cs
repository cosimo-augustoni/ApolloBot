using System.Text.Json.Serialization;

namespace Apollo.Stats.Activity;

/// <summary>
/// The summed activity of a range or a day.
/// </summary>
/// <param name="Messages">Messages sent (edits do not count, a deleted message still counts once).</param>
/// <param name="Characters">Characters of those messages.</param>
/// <param name="ReactionsGiven">Reactions added.</param>
/// <param name="ReactionsReceived">Reactions others added to the messages.</param>
/// <param name="VoiceSeconds">Seconds of closed voice sessions, counted on the Zurich day they started.</param>
public sealed record ActivityCounts(long Messages, long Characters, long ReactionsGiven, long ReactionsReceived, long VoiceSeconds)
{
    public static ActivityCounts Zero { get; } = new(0, 0, 0, 0, 0);

    /// <summary>
    /// Whether these counts include something done, not only received: a message, a reaction or voice time.
    /// </summary>
    [JsonIgnore]
    public bool IsActive => Messages > 0 || ReactionsGiven > 0 || VoiceSeconds > 0;
}
