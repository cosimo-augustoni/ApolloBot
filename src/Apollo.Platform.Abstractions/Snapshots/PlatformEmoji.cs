namespace Apollo.Platform.Abstractions.Snapshots;

/// <summary>
/// A reaction emoji: unicode when <paramref name="CustomEmoji"/> is <see langword="null"/>, custom otherwise.
/// </summary>
/// <param name="Name">The emoji itself for unicode emoji, the custom emoji's name otherwise.</param>
/// <param name="CustomEmoji">The custom emoji, or <see langword="null"/> for a unicode emoji.</param>
public sealed record PlatformEmoji(string Name, PlatformRef? CustomEmoji);
