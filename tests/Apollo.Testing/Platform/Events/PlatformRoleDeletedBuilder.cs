using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Testing.Platform;

public sealed class PlatformRoleDeletedBuilder : PlatformEventBuilder<PlatformRoleDeletedBuilder, PlatformRoleDeleted>
{
    public PlatformRoleDeletedBuilder ForRole(PlatformRef role) => With(e => e with { Role = role });

    protected override PlatformRoleDeleted CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Role: TestPlatform.Role("member"));
}
