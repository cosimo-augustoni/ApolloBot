namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// Who may see a <see cref="CommandReply"/>: a delivery intent that the adapter maps to what its platform offers.
/// </summary>
public enum ReplyVisibility
{
    /// <summary>
    /// Only the member who ran the command (Discord: ephemeral; Fluxer: DM).
    /// </summary>
    Private,

    /// <summary>
    /// Everyone in the channel.
    /// </summary>
    Public,
}
