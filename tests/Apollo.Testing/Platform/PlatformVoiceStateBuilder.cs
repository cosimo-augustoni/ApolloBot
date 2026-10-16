using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformVoiceStateBuilder : TestDataBuilder<PlatformVoiceStateBuilder, PlatformVoiceState>
{
    public PlatformVoiceStateBuilder ForUser(PlatformRef user) => With(v => v with { User = user });

    public PlatformVoiceStateBuilder InChannel(PlatformRef? channel) => With(v => v with { Channel = channel });

    public PlatformVoiceStateBuilder WithConnection(string? connectionId) => With(v => v with { ConnectionId = connectionId });

    public PlatformVoiceStateBuilder Muted() => With(v => v with { SelfMute = true });

    public PlatformVoiceStateBuilder Deafened() => With(v => v with { SelfDeaf = true });

    public PlatformVoiceStateBuilder Streaming() => With(v => v with { Streaming = true });

    public PlatformVoiceStateBuilder WithVideo() => With(v => v with { Video = true });

    protected override PlatformVoiceState CreateDefault() => new(
        User: TestPlatform.User("alice"),
        Channel: TestPlatform.Channel("voice"),
        ConnectionId: null,
        SelfMute: false,
        SelfDeaf: false,
        Streaming: false,
        Video: false);
}
