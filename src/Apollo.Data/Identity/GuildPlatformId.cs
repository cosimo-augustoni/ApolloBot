using Apollo.Platform.Abstractions;

namespace Apollo.Data.Identity;

/// <summary>
/// Maps one platform ID to a <see cref="Guild"/>. A guild can have several mappings (one per platform); a
/// <c>(platform, external_id)</c> pair belongs to exactly one guild and never changes.
/// </summary>
public sealed class GuildPlatformId
{
    public Guid GuildId { get; init; }

    public PlatformKind Platform { get; init; }

    public required string ExternalId { get; init; }
}
