using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformMemberUpdatedBuilder : PlatformEventBuilder<PlatformMemberUpdatedBuilder, PlatformMemberUpdated>
{
    public PlatformMemberUpdatedBuilder WithMember(PlatformMember member) => With(e => e with { Member = member });

    protected override PlatformMemberUpdated CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Member: new PlatformMemberBuilder().Build());
}
