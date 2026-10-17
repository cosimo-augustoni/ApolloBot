namespace Apollo.Domain.Messages;

/// <summary>
/// What a message contains, as bit flags (<c>message_events.content_parts</c>, a <c>smallint</c>). Tells "link only"
/// (<see cref="Link"/>), "mention + link" (<see cref="Mention"/> | <see cref="Link"/>) and "text with a link"
/// (<see cref="Text"/> | <see cref="Link"/>) apart, as well as emoji-only and attachment-only posts. Computed by
/// <see cref="MessageMetadata.From"/>; <c>MessageContentParts.From</c> computes it alone.
/// </summary>
[Flags]
public enum MessageContentParts : short
{
    None = 0,

    /// <summary>
    /// Any characters left after removing links, mentions, emoji and whitespace.
    /// </summary>
    Text = 1,

    /// <summary>
    /// A URL or a channel mention (<c>&lt;#id&gt;</c>).
    /// </summary>
    Link = 2,

    /// <summary>
    /// A user, role, <c>@everyone</c> or <c>@here</c> mention.
    /// </summary>
    Mention = 4,

    /// <summary>
    /// A unicode or custom emoji.
    /// </summary>
    Emoji = 8,

    /// <summary>
    /// At least one attachment.
    /// </summary>
    Attachment = 16,
}
