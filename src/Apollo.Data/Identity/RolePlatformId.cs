using Apollo.Platform.Abstractions;

namespace Apollo.Data.Identity;

/// <summary>
/// Maps one platform ID to a <see cref="Role"/>. A role can have several mappings (one per platform); a
/// <c>(platform, external_id)</c> pair belongs to exactly one role and never changes.
/// </summary>
public sealed class RolePlatformId
{
    public Guid RoleId { get; init; }

    public PlatformKind Platform { get; init; }

    public required string ExternalId { get; init; }
}
