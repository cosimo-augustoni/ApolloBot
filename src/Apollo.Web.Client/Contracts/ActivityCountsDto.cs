using System.Text.Json.Serialization;

namespace Apollo.Web.Client.Contracts;

/// <summary>The summed activity of a range or a day (<see cref="ActivityStatsDto"/>).</summary>
/// <param name="Messages">Messages sent (edits do not count).</param>
/// <param name="Characters">Characters of those messages.</param>
/// <param name="ReactionsGiven">Reactions added.</param>
/// <param name="ReactionsReceived">Reactions others added to the messages.</param>
/// <param name="VoiceSeconds">Seconds of closed voice sessions, counted on the Zurich day they started.</param>
public sealed record ActivityCountsDto(long Messages, long Characters, long ReactionsGiven, long ReactionsReceived, long VoiceSeconds)
{
    /// <summary>Whether nothing at all was counted, given or received.</summary>
    [JsonIgnore]
    public bool IsEmpty => Messages == 0 && Characters == 0 && ReactionsGiven == 0 && ReactionsReceived == 0 && VoiceSeconds == 0;
}
