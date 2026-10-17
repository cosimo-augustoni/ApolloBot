using Apollo.Platform.Abstractions;

namespace Apollo.Testing.Platform;

/// <summary>
/// Readable <see cref="PlatformRef"/>s for tests. Everything is on Discord unless a test says otherwise.
/// </summary>
public static class TestPlatform
{
    /// <summary>
    /// The guild every builder and <see cref="FakeChatPlatform"/> use by default.
    /// </summary>
    public static readonly PlatformRef Guild = new(PlatformKind.Discord, "guild");

    public static PlatformRef User(string id) => new(PlatformKind.Discord, id);

    public static PlatformRef Channel(string id) => new(PlatformKind.Discord, id);

    public static PlatformRef Role(string id) => new(PlatformKind.Discord, id);

    public static PlatformRef Message(string id) => new(PlatformKind.Discord, id);

    public static PlatformRef Emoji(string id) => new(PlatformKind.Discord, id);
}
