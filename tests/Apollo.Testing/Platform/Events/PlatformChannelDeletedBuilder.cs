using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Testing.Platform;

public sealed class PlatformChannelDeletedBuilder : PlatformEventBuilder<PlatformChannelDeletedBuilder, PlatformChannelDeleted>
{
    public PlatformChannelDeletedBuilder ForChannel(PlatformRef channel) => With(e => e with { Channel = channel });

    protected override PlatformChannelDeleted CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Channel: TestPlatform.Channel("general"));
}
