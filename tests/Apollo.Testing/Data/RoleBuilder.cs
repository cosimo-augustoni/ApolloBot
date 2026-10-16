using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Testing.Data;

/// <summary>
/// Builds a <see cref="Role"/> row with its snapshot columns. <see cref="SeedAsync"/> creates the internal ID (and the
/// guild's, if needed) through <see cref="IPlatformIdResolver"/> and stores the columns. Defaults to
/// <c>TestPlatform.Role("member")</c> in <see cref="TestPlatform.Guild"/>.
/// </summary>
public sealed class RoleBuilder : TestDataBuilder<RoleBuilder, Role>
{
    private PlatformRef _guild = TestPlatform.Guild;
    private PlatformRef _role = TestPlatform.Role("member");

    public RoleBuilder For(PlatformRef role, PlatformRef? guild = null)
    {
        _role = role;
        _guild = guild ?? _guild;
        return this;
    }

    public RoleBuilder Named(string name) => With(r =>
    {
        r.Name = name;
        return r;
    });

    public RoleBuilder AtPosition(int position) => With(r =>
    {
        r.Position = position;
        return r;
    });

    public RoleBuilder AsAdministrator() => With(r =>
    {
        r.IsAdministrator = true;
        return r;
    });

    public RoleBuilder WithColor(int? color) => With(r =>
    {
        r.Color = color;
        return r;
    });

    public RoleBuilder DeletedAt(DateTimeOffset deletedAt) => With(r =>
    {
        r.DeletedAt = deletedAt;
        return r;
    });

    public async Task<Role> SeedAsync(ApolloDbContext db, IPlatformIdResolver ids, CancellationToken cancellationToken)
    {
        var id = await ids.ResolveRoleAsync(_guild, _role, cancellationToken);
        var row = await db.Roles.SingleAsync(r => r.Id == id, cancellationToken);
        var values = Build();
        row.Name = values.Name;
        row.Color = values.Color;
        row.Position = values.Position;
        row.IsAdministrator = values.IsAdministrator;
        row.DeletedAt = values.DeletedAt;
        await db.SaveChangesAsync(cancellationToken);
        return row;
    }

    protected override Role CreateDefault() => new() { Name = "Member", Position = 1 };
}
