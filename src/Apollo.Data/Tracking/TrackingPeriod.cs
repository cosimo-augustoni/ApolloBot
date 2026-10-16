namespace Apollo.Data.Tracking;

/// <summary>
/// A span in which a user was tracked (scope §5.1.1), so recaps can say "tracked since …" and leave untracked time out
/// of averages. A user has at most one open period (<see cref="To"/> is <see langword="null"/>) per guild.
/// </summary>
public sealed class TrackingPeriod
{
    public long Id { get; init; }

    public Guid GuildId { get; init; }

    public Guid UserId { get; init; }

    public DateTimeOffset From { get; init; }

    public DateTimeOffset? To { get; set; }
}
