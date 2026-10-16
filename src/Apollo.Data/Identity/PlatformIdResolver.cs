using Apollo.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Apollo.Data.Identity;

/// <summary>
/// Resolves platform IDs against the mapping tables. Creation is one atomic statement per ref, so it is safe across
/// processes (bot and web, scope §4.1) without any lock.
/// </summary>
public sealed class PlatformIdResolver(ApolloDbContext db, PlatformIdCache cache, ILogger<PlatformIdResolver> logger)
    : IPlatformIdResolver
{
    // A concurrent creator that rolls back between our insert and our read makes both come back empty; trying again
    // then inserts. More than a couple of rounds would mean something else is wrong.
    private const int MaxAttempts = 3;

    public Task<Guid> ResolveGuildAsync(PlatformRef guild, CancellationToken cancellationToken) =>
        ResolveAsync(IdentityKind.Guild, guild, guildId: null, cancellationToken);

    public Task<Guid> ResolveUserAsync(PlatformRef user, CancellationToken cancellationToken) =>
        ResolveAsync(IdentityKind.User, user, guildId: null, cancellationToken);

    public async Task<Guid> ResolveChannelAsync(PlatformRef guild, PlatformRef channel, CancellationToken cancellationToken)
    {
        EnsureSamePlatform(guild, channel);
        if (cache.TryGet(IdentityKind.Channel, channel, out var id))
        {
            return id;
        }

        var guildId = await ResolveGuildAsync(guild, cancellationToken);
        return await ResolveAsync(IdentityKind.Channel, channel, guildId, cancellationToken);
    }

    public async Task<Guid> ResolveRoleAsync(PlatformRef guild, PlatformRef role, CancellationToken cancellationToken)
    {
        EnsureSamePlatform(guild, role);
        if (cache.TryGet(IdentityKind.Role, role, out var id))
        {
            return id;
        }

        var guildId = await ResolveGuildAsync(guild, cancellationToken);
        return await ResolveAsync(IdentityKind.Role, role, guildId, cancellationToken);
    }

    public Task<Guid?> TryGetGuildAsync(PlatformRef guild, CancellationToken cancellationToken) =>
        TryGetAsync(IdentityKind.Guild, guild, cancellationToken);

    public Task<Guid?> TryGetUserAsync(PlatformRef user, CancellationToken cancellationToken) =>
        TryGetAsync(IdentityKind.User, user, cancellationToken);

    public Task<Guid?> TryGetChannelAsync(PlatformRef channel, CancellationToken cancellationToken) =>
        TryGetAsync(IdentityKind.Channel, channel, cancellationToken);

    public Task<Guid?> TryGetRoleAsync(PlatformRef role, CancellationToken cancellationToken) =>
        TryGetAsync(IdentityKind.Role, role, cancellationToken);

    private async Task<Guid> ResolveAsync(
        IdentityKind kind,
        PlatformRef platformRef,
        Guid? guildId,
        CancellationToken cancellationToken)
    {
        if (cache.TryGet(kind, platformRef, out var cached))
        {
            return cached;
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var created = await TryCreateAsync(kind, platformRef, guildId, cancellationToken);
            if (created is { } newId)
            {
                logger.LogDebug("Created internal {IdentityKind} {InternalId} for {PlatformRef}", kind, newId, platformRef);
                cache.Add(kind, platformRef, newId);
                return newId;
            }

            if (await TryGetAsync(kind, platformRef, cancellationToken) is { } existingId)
            {
                return existingId;
            }
        }

        throw new InvalidOperationException($"Could not resolve {kind} {platformRef} after {MaxAttempts} attempts.");
    }

    private async Task<Guid?> TryGetAsync(IdentityKind kind, PlatformRef platformRef, CancellationToken cancellationToken)
    {
        if (cache.TryGet(kind, platformRef, out var cached))
        {
            return cached;
        }

        var (platform, externalId) = (platformRef.Platform, platformRef.ExternalId);
        var id = kind switch
        {
            IdentityKind.Guild => await db.GuildPlatformIds.AsNoTracking()
                .Where(x => x.Platform == platform && x.ExternalId == externalId)
                .Select(x => (Guid?)x.GuildId)
                .SingleOrDefaultAsync(cancellationToken),
            IdentityKind.User => await db.UserPlatformIds.AsNoTracking()
                .Where(x => x.Platform == platform && x.ExternalId == externalId)
                .Select(x => (Guid?)x.UserId)
                .SingleOrDefaultAsync(cancellationToken),
            IdentityKind.Channel => await db.ChannelPlatformIds.AsNoTracking()
                .Where(x => x.Platform == platform && x.ExternalId == externalId)
                .Select(x => (Guid?)x.ChannelId)
                .SingleOrDefaultAsync(cancellationToken),
            IdentityKind.Role => await db.RolePlatformIds.AsNoTracking()
                .Where(x => x.Platform == platform && x.ExternalId == externalId)
                .Select(x => (Guid?)x.RoleId)
                .SingleOrDefaultAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        if (id is { } found)
        {
            cache.Add(kind, platformRef, found);
        }

        return id;
    }

    /// <summary>
    /// Inserts the mapping and the entity row for a new ID, or nothing when the ref is already mapped.
    /// </summary>
    /// <returns>The new ID, or <see langword="null"/> when another call created the mapping first.</returns>
    private async Task<Guid?> TryCreateAsync(
        IdentityKind kind,
        PlatformRef platformRef,
        Guid? guildId,
        CancellationToken cancellationToken)
    {
        var rows = await db.Database
            .SqlQuery<Guid>(InsertSql(kind, Guid.CreateVersion7(), platformRef, guildId))
            .ToListAsync(cancellationToken);
        return rows.Count == 0 ? null : rows[0];
    }

    // Raw SQL because EF has no INSERT ... ON CONFLICT DO NOTHING (issue #11). The mapping insert runs first: when the
    // (platform, external_id) key exists it inserts nothing, waiting for a concurrent uncommitted insert of the same key
    // if there is one, and then the entity insert has no row to copy either. Both inserts are one statement, so the
    // foreign key from the mapping to the entity row is checked only after both ran, and no half-created identity can
    // be left behind.
    private static FormattableString InsertSql(IdentityKind kind, Guid id, PlatformRef platformRef, Guid? guildId)
    {
        var platform = PlatformKindConverter.ToText(platformRef.Platform);
        var externalId = platformRef.ExternalId;
        return kind switch
        {
            IdentityKind.Guild => $"""
                WITH mapping AS (
                    INSERT INTO guild_platform_ids (guild_id, platform, external_id) VALUES ({id}, {platform}, {externalId})
                    ON CONFLICT (platform, external_id) DO NOTHING
                    RETURNING guild_id),
                created AS (INSERT INTO guilds (id) SELECT guild_id FROM mapping)
                SELECT guild_id AS "Value" FROM mapping
                """,
            IdentityKind.User => $"""
                WITH mapping AS (
                    INSERT INTO user_platform_ids (user_id, platform, external_id) VALUES ({id}, {platform}, {externalId})
                    ON CONFLICT (platform, external_id) DO NOTHING
                    RETURNING user_id),
                created AS (INSERT INTO users (id) SELECT user_id FROM mapping)
                SELECT user_id AS "Value" FROM mapping
                """,
            IdentityKind.Channel => $"""
                WITH mapping AS (
                    INSERT INTO channel_platform_ids (channel_id, platform, external_id) VALUES ({id}, {platform}, {externalId})
                    ON CONFLICT (platform, external_id) DO NOTHING
                    RETURNING channel_id),
                created AS (INSERT INTO channels (id, guild_id) SELECT channel_id, {guildId} FROM mapping)
                SELECT channel_id AS "Value" FROM mapping
                """,
            IdentityKind.Role => $"""
                WITH mapping AS (
                    INSERT INTO role_platform_ids (role_id, platform, external_id) VALUES ({id}, {platform}, {externalId})
                    ON CONFLICT (platform, external_id) DO NOTHING
                    RETURNING role_id),
                created AS (INSERT INTO roles (id, guild_id) SELECT role_id, {guildId} FROM mapping)
                SELECT role_id AS "Value" FROM mapping
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private static void EnsureSamePlatform(PlatformRef guild, PlatformRef child)
    {
        if (guild.Platform != child.Platform)
        {
            throw new ArgumentException($"{child} cannot belong to guild {guild} on another platform.", nameof(child));
        }
    }
}
