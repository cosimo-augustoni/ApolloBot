namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// A button under a <see cref="CommandReply"/>. Using it delivers a <see cref="ButtonInvocation"/> with this
/// <paramref name="Id"/> to the <see cref="IButtonHandler"/> of its prefix.
/// </summary>
/// <param name="Id">Built with <see cref="ButtonId"/>, so it names the handler and the member it is for.</param>
/// <param name="Label">The button text (German).</param>
/// <param name="Style">How prominent the button is.</param>
public sealed record ReplyButton(string Id, string Label, ButtonStyle Style);
