using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformRoleBuilder : TestDataBuilder<PlatformRoleBuilder, PlatformRole>
{
    public PlatformRoleBuilder WithRef(PlatformRef role) => With(r => r with { Role = role });

    public PlatformRoleBuilder Named(string name) => With(r => r with { Name = name });

    public PlatformRoleBuilder AsAdministrator() => With(r => r with { IsAdministrator = true });

    protected override PlatformRole CreateDefault() => new(
        Role: TestPlatform.Role("member"),
        Name: "Member",
        Position: 1,
        IsAdministrator: false,
        IsManaged: false);
}
