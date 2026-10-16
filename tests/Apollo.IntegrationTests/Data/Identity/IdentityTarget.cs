using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Apollo.IntegrationTests.Data.Identity;

/// <summary>
/// One kind of internal identity (guild, user, channel, role), so the resolver tests run the same behaviour against all
/// four. Channels and roles are resolved in the guild <c>"guild"</c> on the ref's own platform.
/// </summary>
public sealed record IdentityTarget(
    string Name,
    Func<IPlatformIdResolver, PlatformRef, CancellationToken, Task<Guid>> Resolve,
    Func<IPlatformIdResolver, PlatformRef, CancellationToken, Task<Guid?>> TryGet,
    Func<ApolloDbContext, IQueryable<Guid>> Ids,
    Func<ApolloDbContext, IQueryable<Guid>> MappedIds)
{
    public static readonly IdentityTarget Guild = new(
        "guild",
        (resolver, guild, ct) => resolver.ResolveGuildAsync(guild, ct),
        (resolver, guild, ct) => resolver.TryGetGuildAsync(guild, ct),
        db => db.Guilds.Select(x => x.Id),
        db => db.GuildPlatformIds.Select(x => x.GuildId));

    public static readonly IdentityTarget User = new(
        "user",
        (resolver, user, ct) => resolver.ResolveUserAsync(user, ct),
        (resolver, user, ct) => resolver.TryGetUserAsync(user, ct),
        db => db.Users.Select(x => x.Id),
        db => db.UserPlatformIds.Select(x => x.UserId));

    public static readonly IdentityTarget Channel = new(
        "channel",
        (resolver, channel, ct) => resolver.ResolveChannelAsync(GuildOf(channel), channel, ct),
        (resolver, channel, ct) => resolver.TryGetChannelAsync(channel, ct),
        db => db.Channels.Select(x => x.Id),
        db => db.ChannelPlatformIds.Select(x => x.ChannelId));

    public static readonly IdentityTarget Role = new(
        "role",
        (resolver, role, ct) => resolver.ResolveRoleAsync(GuildOf(role), role, ct),
        (resolver, role, ct) => resolver.TryGetRoleAsync(role, ct),
        db => db.Roles.Select(x => x.Id),
        db => db.RolePlatformIds.Select(x => x.RoleId));

    private static readonly Dictionary<string, IdentityTarget> _byName =
        new[] { Guild, User, Channel, Role }.ToDictionary(target => target.Name);

    /// <summary>
    /// Theory data: the names of all targets (strings keep the test cases serializable).
    /// </summary>
    public static TheoryData<string> All => new(_byName.Keys);

    public static IdentityTarget Named(string name) => _byName[name];

    public static PlatformRef GuildOf(PlatformRef child) => new(child.Platform, "guild");

    /// <summary>
    /// The IDs in the entity table (<c>users</c>, ...).
    /// </summary>
    public Task<List<Guid>> ReadIdsAsync(ApolloDbContext db) =>
        Ids(db).ToListAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// The internal IDs the mapping table points to, one per mapping row.
    /// </summary>
    public Task<List<Guid>> ReadMappedIdsAsync(ApolloDbContext db) =>
        MappedIds(db).ToListAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Deletes every mapping row (the entity rows stay).
    /// </summary>
    public Task<int> DeleteMappingsAsync(ApolloDbContext db) =>
        Name switch
        {
            "guild" => db.GuildPlatformIds.ExecuteDeleteAsync(TestContext.Current.CancellationToken),
            "user" => db.UserPlatformIds.ExecuteDeleteAsync(TestContext.Current.CancellationToken),
            "channel" => db.ChannelPlatformIds.ExecuteDeleteAsync(TestContext.Current.CancellationToken),
            _ => db.RolePlatformIds.ExecuteDeleteAsync(TestContext.Current.CancellationToken),
        };

    public override string ToString() => Name;
}
