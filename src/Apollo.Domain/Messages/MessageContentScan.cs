using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Apollo.Domain.Messages;

/// <summary>
/// One pass over a message's text that keeps only counts and flags. The text itself never leaves <see cref="Run"/>.
/// </summary>
internal sealed partial class MessageContentScan
{
    public required int Length { get; init; }

    public required int WordCount { get; init; }

    public required int EmojiCount { get; init; }

    public required int UserMentionCount { get; init; }

    public required int RoleMentionCount { get; init; }

    public required bool MentionsEveryone { get; init; }

    public required bool HasLink { get; init; }

    public required bool HasText { get; init; }

    /// <summary>
    /// Scans <paramref name="content"/>: links, mentions and custom emoji are matched as markup, unicode emoji per
    /// character, and whatever is left is text.
    /// </summary>
    /// <param name="content">The message text.</param>
    /// <param name="everyoneIsMention">
    /// Whether <c>@everyone</c> and <c>@here</c> count as mentions. Discord pings only when the author may, so its payload
    /// can say they are plain text.
    /// </param>
    public static MessageContentScan Run(string content, bool everyoneIsMention)
    {
        var userMentions = new HashSet<string>(StringComparer.Ordinal);
        var roleMentions = new HashSet<string>(StringComparer.Ordinal);
        var mentionsEveryone = false;
        var hasLink = false;
        var emojiCount = 0;

        // Custom emoji and mentions show as one name or icon, so each counts as one character; markup is plain ASCII.
        var markupOverhead = 0;

        var rest = Markup().Replace(content, match =>
        {
            if (match.Groups["user"].Success)
            {
                userMentions.Add(match.Groups["user"].Value);
                markupOverhead += match.Length - 1;
            }
            else if (match.Groups["role"].Success)
            {
                roleMentions.Add(match.Groups["role"].Value);
                markupOverhead += match.Length - 1;
            }
            else if (match.Groups["channel"].Success)
            {
                hasLink = true;
                markupOverhead += match.Length - 1;
            }
            else if (match.Groups["everyone"].Success)
            {
                if (!everyoneIsMention)
                {
                    return match.Value;
                }

                mentionsEveryone = true;
            }
            else if (match.Groups["emoji"].Success)
            {
                emojiCount++;
                markupOverhead += match.Length - 1;
            }
            else
            {
                hasLink = true;
            }

            return " ";
        });

        var hasText = false;
        var text = new StringBuilder(rest.Length);
        var elements = StringInfo.GetTextElementEnumerator(rest);
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            if (IsEmoji(element))
            {
                emojiCount++;
                text.Append(' ');
            }
            else
            {
                hasText |= !string.IsNullOrWhiteSpace(element) && !IsFormatting(element);
                text.Append(element);
            }
        }

        return new MessageContentScan
        {
            Length = new StringInfo(content).LengthInTextElements - markupOverhead,
            WordCount = text.ToString()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Count(word => word.Any(char.IsLetterOrDigit)),
            EmojiCount = emojiCount,
            UserMentionCount = userMentions.Count,
            RoleMentionCount = roleMentions.Count,
            MentionsEveryone = mentionsEveryone,
            HasLink = hasLink,
            HasText = hasText,
        };
    }

    /// <summary>
    /// Whether a text element (one user-perceived character) is an emoji. An approximation without an emoji table: a
    /// keycap or emoji presentation selector, or a first code point in the emoji blocks (pictographs, symbols, dingbats,
    /// flags). Plain <c>©</c> or digits are text, as Discord renders them.
    /// </summary>
    private static bool IsEmoji(string element)
    {
        if (element.Contains('\u20E3', StringComparison.Ordinal) || element.Contains('\uFE0F', StringComparison.Ordinal))
        {
            return true;
        }

        var first = Rune.GetRuneAt(element, 0).Value;
        return first is (>= 0x1F000 and <= 0x1FAFF) or (>= 0x2600 and <= 0x27BF) or (>= 0x2300 and <= 0x23FF)
            or (>= 0x2B00 and <= 0x2BFF);
    }

    /// <summary>
    /// Discord formatting characters (bold, italics, underline, strikethrough, spoiler, code, quote): not text.
    /// </summary>
    private static bool IsFormatting(string element) => element is "*" or "_" or "~" or "|" or "`" or ">";

    // Order matters: a custom emoji or mention is markup in angle brackets, a URL may be wrapped in them to hide its
    // embed. A URL stops before formatting characters and keeps brackets only in balanced pairs, as in
    // ".../Apollo_(program)"; a trailing underscore or punctuation is not part of it. Brackets right around a link
    // belong to the link. @everyone and @here are case-sensitive whole words, as Discord pings them.
    [GeneratedRegex("""
        <a?:\w+:(?<emoji>\d+)>
        | <@!?(?<user>\d+)>
        | <@&(?<role>\d+)>
        | (?<channel><\#\d+>)
        | [(\[]? <(?i:https?)://[^\s>]+> [)\]]?
        | [(\[]? (?i:https?):// (?: [^\s<>*~|`()\[\]] | \([^\s<>*~|`()]*\) )+ (?<![_.,:;!?'"]) [)\]]?
        | (?<everyone>(?<!\w)@(?:everyone|here)(?!\w))
        """, RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant)]
    private static partial Regex Markup();
}
