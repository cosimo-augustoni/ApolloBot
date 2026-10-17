namespace Apollo.Data.Messages;

/// <summary>
/// What happened to a message (<c>message_events.kind</c>).
/// </summary>
public enum MessageEventKind
{
    Create,
    Edit,
    Delete,
}
