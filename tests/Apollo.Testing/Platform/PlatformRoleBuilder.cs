using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformRoleBuilder : TestDataBuilder<PlatformRoleBuilder, PlatformRole>
{
    public PlatformRoleBuilder WithRef(PlatformRef role) => With(r => r with { Role = role });

    public PlatformRoleBuilder Named(string name) => With(r => r with { Name = name });

    public PlatformRoleBuilder AsAdministrator(bool isAdministrator = true) => With(r => r with { IsAdministrator = isAdministrator });

    public PlatformRoleBuilder ManagingRoles(bool managesRoles = true) => With(r => r with { ManagesRoles = managesRoles });

    public PlatformRoleBuilder AtPosition(int position) => With(r => r with { Position = position });

    public PlatformRoleBuilder WithColor(int? color) => With(r => r with { Color = color });

    protected override PlatformRole CreateDefault() => new(
        Role: TestPlatform.Role("member"),
        Name: "Member",
        Position: 1,
        IsAdministrator: false,
        ManagesRoles: false,
        IsManaged: false,
        Color: null);
}
