using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Testing.Platform;

public sealed class PlatformConnectionStateChangedBuilder : PlatformEventBuilder<PlatformConnectionStateChangedBuilder, PlatformConnectionStateChanged>
{
    public PlatformConnectionStateChangedBuilder WithState(PlatformConnectionState state) => With(e => e with { State = state });

    protected override PlatformConnectionStateChanged CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        State: PlatformConnectionState.Connected);
}
