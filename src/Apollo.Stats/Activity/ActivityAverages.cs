namespace Apollo.Stats.Activity;

/// <summary>
/// <see cref="ActivityCounts"/> per day: for the guild over every day of the range, for a user over their tracked
/// days only (scope §5.1.1), and all zero when there is no day to divide by.
/// </summary>
public sealed record ActivityAverages(double Messages, double Characters, double ReactionsGiven, double ReactionsReceived, double VoiceSeconds)
{
    public static ActivityAverages Zero { get; } = new(0, 0, 0, 0, 0);
}
