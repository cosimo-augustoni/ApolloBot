using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformVoiceStateChangedBuilder : PlatformEventBuilder<PlatformVoiceStateChangedBuilder, PlatformVoiceStateChanged>
{
    public PlatformVoiceStateChangedBuilder WithState(PlatformVoiceState state) => With(e => e with { State = state });

    protected override PlatformVoiceStateChanged CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        State: new PlatformVoiceStateBuilder().Build());
}
