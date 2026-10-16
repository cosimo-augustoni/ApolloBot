using Apollo.Domain.Messages;

namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A message was edited. Carries the recomputed metadata only, never the content.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="Channel">The channel or thread.</param>
/// <param name="Message">The message.</param>
/// <param name="Author">The author when the payload has it (partial updates may not); otherwise the recorder looks it up.</param>
/// <param name="Metadata">What the adapter computed from the edited content.</param>
public sealed record PlatformMessageUpdated(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformRef Channel,
    PlatformRef Message,
    PlatformRef? Author,
    MessageMetadata Metadata)
    : PlatformEvent(Guild, OccurredAt);
