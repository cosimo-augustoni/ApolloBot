using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Testing.Platform;

/// <summary>
/// Base for platform event builders. Defaults are <see cref="TestPlatform.Guild"/> and <see cref="TestTime.Now"/>.
/// </summary>
public abstract class PlatformEventBuilder<TBuilder, TEvent> : TestDataBuilder<TBuilder, TEvent>
    where TBuilder : PlatformEventBuilder<TBuilder, TEvent>
    where TEvent : PlatformEvent
{
    public TBuilder InGuild(PlatformRef guild) => With(e => (TEvent)((PlatformEvent)e with { Guild = guild }));

    public TBuilder At(DateTimeOffset occurredAt) => With(e => (TEvent)((PlatformEvent)e with { OccurredAt = occurredAt }));
}
