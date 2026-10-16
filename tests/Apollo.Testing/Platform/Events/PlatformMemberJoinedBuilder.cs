using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformMemberJoinedBuilder : PlatformEventBuilder<PlatformMemberJoinedBuilder, PlatformMemberJoined>
{
    public PlatformMemberJoinedBuilder WithMember(PlatformMember member) => With(e => e with { Member = member });

    protected override PlatformMemberJoined CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Member: new PlatformMemberBuilder().Build());
}
