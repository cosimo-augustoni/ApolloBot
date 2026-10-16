using Apollo.Platform.Abstractions.Commands;
using NetCord;
using NetCord.Rest;
using ButtonStyle = Apollo.Platform.Abstractions.Commands.ButtonStyle;

namespace Apollo.Platform.Discord;

/// <summary>
/// Turns a neutral <see cref="CommandReply"/> into an interaction response: <see cref="ReplyVisibility.Private"/>
/// becomes the ephemeral flag, buttons become Discord buttons. Link previews are suppressed, so the privacy footer
/// stays a plain link.
/// </summary>
internal static class DiscordReplies
{
    /// <summary>
    /// The flags of a deferred response. Discord fixes the visibility when deferring, before the handler has replied,
    /// so a deferred reply is always ephemeral (every <c>/apollo</c> reply is private anyway).
    /// </summary>
    public const MessageFlags DeferredFlags = MessageFlags.Ephemeral;

    // Discord allows at most five buttons per action row.
    private const int ButtonsPerRow = 5;

    public static InteractionMessageProperties ToMessage(CommandReply reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        return new InteractionMessageProperties
        {
            Content = reply.Text,
            Flags = reply.Visibility == ReplyVisibility.Private
                ? MessageFlags.Ephemeral | MessageFlags.SuppressEmbeds
                : MessageFlags.SuppressEmbeds,
            Components = ToComponents(reply),
        };
    }

    /// <summary>
    /// Fills the edit of a deferred response. Its visibility was fixed by <see cref="DeferredFlags"/>.
    /// </summary>
    public static void Apply(MessageOptions message, CommandReply reply)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(reply);
        message.Content = reply.Text;
        message.Flags = MessageFlags.SuppressEmbeds;
        message.Components = ToComponents(reply);
    }

    private static List<IMessageComponentProperties> ToComponents(CommandReply reply) =>
    [
        .. reply.Buttons.Chunk(ButtonsPerRow).Select(row => new ActionRowProperties(row.Select(ToButton))),
    ];

    private static ButtonProperties ToButton(ReplyButton button) => new(button.Id, button.Label, button.Style switch
    {
        ButtonStyle.Primary => NetCord.ButtonStyle.Primary,
        ButtonStyle.Secondary => NetCord.ButtonStyle.Secondary,
        ButtonStyle.Success => NetCord.ButtonStyle.Success,
        ButtonStyle.Danger => NetCord.ButtonStyle.Danger,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button.Style, "Unknown button style."),
    });
}
