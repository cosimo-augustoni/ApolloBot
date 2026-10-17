namespace Apollo.Data.Membership;

/// <summary>
/// A tracked user joined, left, was banned or was unbanned (scope §5.1). Append-only.
/// </summary>
public sealed class MembershipEvent
{
    public long Id { get; init; }

    public Guid GuildId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public Guid UserId { get; init; }

    public MembershipEventKind Kind { get; init; }
}
