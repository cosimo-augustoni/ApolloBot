using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Testing.Platform;

public sealed class PlatformMemberBannedBuilder : PlatformEventBuilder<PlatformMemberBannedBuilder, PlatformMemberBanned>
{
    public PlatformMemberBannedBuilder ForUser(PlatformRef user) => With(e => e with { User = user });

    protected override PlatformMemberBanned CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        User: TestPlatform.User("alice"));
}
