namespace Apollo.Domain.Names;

/// <summary>
/// A row <see cref="MemberNameHistory.Next"/> asks to store.
/// </summary>
public sealed record MemberNameEntry(MemberNameChangeKind Kind, MemberNames Names, DateTimeOffset At);
