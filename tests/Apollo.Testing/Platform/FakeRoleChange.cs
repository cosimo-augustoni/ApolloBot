using Apollo.Platform.Abstractions;

namespace Apollo.Testing.Platform;

/// <summary>
/// A role call the bot made on <see cref="FakeChatPlatform"/>.
/// </summary>
public sealed record FakeRoleChange(PlatformRef User, PlatformRef Role, FakeRoleChangeKind Kind);
