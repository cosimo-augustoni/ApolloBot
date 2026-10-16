using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Commands;

namespace Apollo.Testing.Platform;

/// <summary>
/// A reply <see cref="FakeChatPlatform"/> delivered to <paramref name="Member"/> after a command or button.
/// </summary>
public sealed record FakeReply(PlatformRef Member, CommandReply Reply);
