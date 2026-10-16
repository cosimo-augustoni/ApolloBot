using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformRoleCreatedBuilder : PlatformEventBuilder<PlatformRoleCreatedBuilder, PlatformRoleCreated>
{
    public PlatformRoleCreatedBuilder WithRole(PlatformRole role) => With(e => e with { Role = role });

    protected override PlatformRoleCreated CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Role: new PlatformRoleBuilder().Build());
}
