namespace Apollo.Web.Client.Components.Stats;

/// <summary>
/// What a stats component shows when it can show voice time or messages. Voice comes first (#111): the server is used
/// far more for voice than for text, so it is every toggle's default.
/// </summary>
public enum StatsMetric
{
    VoiceHours,
    Messages,
}
