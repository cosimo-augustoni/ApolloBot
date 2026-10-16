using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformChannelCreatedBuilder : PlatformEventBuilder<PlatformChannelCreatedBuilder, PlatformChannelCreated>
{
    public PlatformChannelCreatedBuilder WithChannel(PlatformChannel channel) => With(e => e with { Channel = channel });

    protected override PlatformChannelCreated CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Channel: new PlatformChannelBuilder().Build());
}
