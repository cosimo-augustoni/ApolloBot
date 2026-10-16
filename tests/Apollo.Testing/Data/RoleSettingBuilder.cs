using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Tracking;
using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Testing.Platform;

namespace Apollo.Testing.Data;

/// <summary>
/// Builds a <see cref="RoleSetting"/> row, by default the tracking role <c>TestPlatform.Role("statistik")</c> in
/// <see cref="TestPlatform.Guild"/>. <see cref="SeedAsync"/> resolves the guild and role through
/// <see cref="IPlatformIdResolver"/> and inserts the row; the database trigger writes its audit entry.
/// </summary>
public sealed class RoleSettingBuilder : TestDataBuilder<RoleSettingBuilder, RoleSetting>
{
    private PlatformRef _guild = TestPlatform.Guild;
    private PlatformRef _role = TestPlatform.Role("statistik");

    public RoleSettingBuilder ForRole(PlatformRef role, PlatformRef? guild = null)
    {
        _role = role;
        _guild = guild ?? _guild;
        return this;
    }

    public RoleSettingBuilder WithPurpose(RoleSettingPurpose purpose) => With(s => Copy(s, purpose: purpose));

    public RoleSettingBuilder ChangedBy(Guid? userId) => With(s => Copy(s, changedByUserId: userId));

    public RoleSettingBuilder ChangedAt(DateTimeOffset changedAt) => With(s => Copy(s, changedAt: changedAt));

    public async Task<RoleSetting> SeedAsync(ApolloDbContext db, IPlatformIdResolver ids, CancellationToken cancellationToken)
    {
        var values = Build();
        var row = new RoleSetting
        {
            GuildId = await ids.ResolveGuildAsync(_guild, cancellationToken),
            Platform = _role.Platform,
            RoleId = await ids.ResolveRoleAsync(_guild, _role, cancellationToken),
            Purpose = values.Purpose,
            ChangedByUserId = values.ChangedByUserId,
            ChangedAt = values.ChangedAt,
        };
        db.RoleSettings.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row;
    }

    protected override RoleSetting CreateDefault() => new() { Purpose = RoleSettingPurpose.Tracking, ChangedAt = TestTime.Now };

    private static RoleSetting Copy(
        RoleSetting source,
        RoleSettingPurpose? purpose = null,
        Guid? changedByUserId = null,
        DateTimeOffset? changedAt = null) => new()
        {
            Purpose = purpose ?? source.Purpose,
            ChangedByUserId = changedByUserId ?? source.ChangedByUserId,
            ChangedAt = changedAt ?? source.ChangedAt,
        };
}
