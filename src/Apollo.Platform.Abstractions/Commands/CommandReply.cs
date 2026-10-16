namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// What a handler answers. The adapter decides how to deliver it: Discord as an interaction response, Fluxer as a DM
/// for <see cref="ReplyVisibility.Private"/>. Handlers must not assume the reply is attached to the invocation.
/// </summary>
/// <param name="Text">The reply text (German). Must read well on its own, e.g. in a DM.</param>
/// <param name="Buttons">Buttons under the text, in order; empty for none.</param>
/// <param name="Visibility">Who may see the reply. Every <c>/apollo</c> reply is private (scope §5.4).</param>
public sealed record CommandReply(
    string Text,
    IReadOnlyList<ReplyButton> Buttons,
    ReplyVisibility Visibility = ReplyVisibility.Private)
{
    /// <summary>
    /// A private reply without buttons.
    /// </summary>
    public static CommandReply Private(string text) => new(text, []);
}
