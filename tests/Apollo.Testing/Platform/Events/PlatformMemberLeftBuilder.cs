using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Testing.Platform;

public sealed class PlatformMemberLeftBuilder : PlatformEventBuilder<PlatformMemberLeftBuilder, PlatformMemberLeft>
{
    public PlatformMemberLeftBuilder ForUser(PlatformRef user) => With(e => e with { User = user });

    protected override PlatformMemberLeft CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        User: TestPlatform.User("alice"));
}
