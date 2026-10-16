using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformRoleUpdatedBuilder : PlatformEventBuilder<PlatformRoleUpdatedBuilder, PlatformRoleUpdated>
{
    public PlatformRoleUpdatedBuilder WithRole(PlatformRole role) => With(e => e with { Role = role });

    protected override PlatformRoleUpdated CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Role: new PlatformRoleBuilder().Build());
}
