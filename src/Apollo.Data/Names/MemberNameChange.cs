using Apollo.Domain.Names;

namespace Apollo.Data.Names;

/// <summary>
/// A tracked member's names after a change, or at the start of their tracking (#114, scope §5.1). Append-only.
/// </summary>
public sealed class MemberNameChange
{
    public long Id { get; init; }

    public Guid GuildId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public Guid UserId { get; init; }

    /// <summary>The server nickname; <see langword="null"/> without one.</summary>
    public string? Nickname { get; init; }

    /// <summary>The global display name; <see langword="null"/> without one.</summary>
    public string? GlobalName { get; init; }

    public required string Username { get; init; }

    public MemberNameChangeKind Kind { get; init; }
}
