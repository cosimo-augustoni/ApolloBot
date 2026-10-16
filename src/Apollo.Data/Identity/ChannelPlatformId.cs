using Apollo.Platform.Abstractions;

namespace Apollo.Data.Identity;

/// <summary>
/// Maps one platform ID to a <see cref="Channel"/>. A channel can have several mappings (one per platform); a
/// <c>(platform, external_id)</c> pair belongs to exactly one channel and never changes.
/// </summary>
public sealed class ChannelPlatformId
{
    public Guid ChannelId { get; init; }

    public PlatformKind Platform { get; init; }

    public required string ExternalId { get; init; }
}
