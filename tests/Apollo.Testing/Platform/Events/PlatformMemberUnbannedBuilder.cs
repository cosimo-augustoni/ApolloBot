using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Testing.Platform;

public sealed class PlatformMemberUnbannedBuilder : PlatformEventBuilder<PlatformMemberUnbannedBuilder, PlatformMemberUnbanned>
{
    public PlatformMemberUnbannedBuilder ForUser(PlatformRef user) => With(e => e with { User = user });

    protected override PlatformMemberUnbanned CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        User: TestPlatform.User("alice"));
}
