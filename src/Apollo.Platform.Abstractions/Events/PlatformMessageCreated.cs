using Apollo.Domain.Messages;

namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A message was sent. Carries metadata only; the content never leaves the adapter (scope §5.1).
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="Channel">The channel or thread the message was sent in.</param>
/// <param name="Message">The message.</param>
/// <param name="Author">The author.</param>
/// <param name="AuthorIsBot">Whether the author is a bot account.</param>
/// <param name="ReplyToMessage">The message this one replies to, if it is a reply.</param>
/// <param name="ReplyToAuthor">The author of the replied-to message when the payload has it; otherwise the recorder looks it up.</param>
/// <param name="MentionedUsers">
/// The distinct users the message mentions in its text, for the mention graph (<c>message_mentions</c>). A reply's
/// automatic ping of the replied-to author is not a mention.
/// </param>
/// <param name="Metadata">What the adapter computed from the content.</param>
public sealed record PlatformMessageCreated(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformRef Channel,
    PlatformRef Message,
    PlatformRef Author,
    bool AuthorIsBot,
    PlatformRef? ReplyToMessage,
    PlatformRef? ReplyToAuthor,
    IReadOnlyList<PlatformRef> MentionedUsers,
    MessageMetadata Metadata)
    : PlatformEvent(Guild, OccurredAt);
