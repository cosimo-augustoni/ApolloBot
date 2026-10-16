namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// How prominent a <see cref="ReplyButton"/> is.
/// </summary>
public enum ButtonStyle
{
    Primary,
    Secondary,
    Success,

    /// <summary>
    /// For actions that cannot be undone, such as deleting data.
    /// </summary>
    Danger,
}
