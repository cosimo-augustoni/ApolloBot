using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformPresenceUpdatedBuilder : PlatformEventBuilder<PlatformPresenceUpdatedBuilder, PlatformPresenceUpdated>
{
    public PlatformPresenceUpdatedBuilder WithPresence(PlatformPresence presence) => With(e => e with { Presence = presence });

    protected override PlatformPresenceUpdated CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Presence: new PlatformPresenceBuilder().Build());
}
