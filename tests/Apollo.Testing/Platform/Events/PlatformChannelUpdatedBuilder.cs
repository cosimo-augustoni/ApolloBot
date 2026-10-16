using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformChannelUpdatedBuilder : PlatformEventBuilder<PlatformChannelUpdatedBuilder, PlatformChannelUpdated>
{
    public PlatformChannelUpdatedBuilder WithChannel(PlatformChannel channel) => With(e => e with { Channel = channel });

    protected override PlatformChannelUpdated CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Channel: new PlatformChannelBuilder().Build());
}
