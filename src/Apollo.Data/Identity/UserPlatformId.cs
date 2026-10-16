using Apollo.Platform.Abstractions;

namespace Apollo.Data.Identity;

/// <summary>
/// Maps one platform ID to a <see cref="User"/>. A user can have several mappings (one per platform); a
/// <c>(platform, external_id)</c> pair belongs to exactly one user and never changes.
/// </summary>
public sealed class UserPlatformId
{
    public Guid UserId { get; init; }

    public PlatformKind Platform { get; init; }

    public required string ExternalId { get; init; }
}
