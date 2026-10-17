namespace Apollo.Domain.Messages;

/// <summary>
/// What ApolloBot keeps about a message's content: counts and flags, never the text (scope §5.1). Adapters compute it
/// in memory with <see cref="From"/> and drop the content.
/// </summary>
public sealed record MessageMetadata
{
    public static MessageMetadata Empty { get; } = new();

    /// <summary>
    /// The length in characters as people see them: an emoji (unicode or custom) and a user, role or channel mention
    /// each count as one; links and all other text keep their characters (text elements).
    /// </summary>
    public int Length { get; init; }

    public int WordCount { get; init; }

    public int AttachmentCount { get; init; }

    public int EmbedCount { get; init; }

    public int EmojiCount { get; init; }

    public int UserMentionCount { get; init; }

    public int RoleMentionCount { get; init; }

    public bool MentionsEveryone { get; init; }

    public MessageContentParts ContentParts { get; init; }

    /// <summary>
    /// Computes the metadata of a message. The content is used only inside this call and never stored. Mention facts the
    /// platform payload has win over parsing the text; pass <see langword="null"/> where the payload lacks them.
    /// </summary>
    /// <param name="content">The message text (empty for attachment-only messages).</param>
    /// <param name="attachmentCount">How many attachments the message has.</param>
    /// <param name="embedCount">How many embeds the message has.</param>
    /// <param name="userMentionCount">How many distinct users the payload says are mentioned; else parsed from <c>&lt;@id&gt;</c>.</param>
    /// <param name="roleMentionCount">How many distinct roles the payload says are mentioned; else parsed from <c>&lt;@&amp;id&gt;</c>.</param>
    /// <param name="mentionsEveryone">Whether the payload says <c>@everyone</c> or <c>@here</c> pinged; else parsed.</param>
    public static MessageMetadata From(
        string content,
        int attachmentCount,
        int embedCount,
        int? userMentionCount = null,
        int? roleMentionCount = null,
        bool? mentionsEveryone = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        var scan = MessageContentScan.Run(content, everyoneIsMention: mentionsEveryone ?? true);
        var users = userMentionCount ?? scan.UserMentionCount;
        var roles = roleMentionCount ?? scan.RoleMentionCount;
        var everyone = mentionsEveryone ?? scan.MentionsEveryone;

        var parts = MessageContentParts.None;
        if (scan.HasText)
        {
            parts |= MessageContentParts.Text;
        }

        if (scan.HasLink)
        {
            parts |= MessageContentParts.Link;
        }

        if (users > 0 || roles > 0 || everyone)
        {
            parts |= MessageContentParts.Mention;
        }

        if (scan.EmojiCount > 0)
        {
            parts |= MessageContentParts.Emoji;
        }

        if (attachmentCount > 0)
        {
            parts |= MessageContentParts.Attachment;
        }

        return new MessageMetadata
        {
            Length = scan.Length,
            WordCount = scan.WordCount,
            AttachmentCount = attachmentCount,
            EmbedCount = embedCount,
            EmojiCount = scan.EmojiCount,
            UserMentionCount = users,
            RoleMentionCount = roles,
            MentionsEveryone = everyone,
            ContentParts = parts,
        };
    }
}
