using Apollo.Data;
using Apollo.Data.Tracking;
using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Bot.Tracking;

/// <summary>
/// Reads what the tracking gate decides on from <c>role_settings</c>, <c>guild_members</c> and <c>member_roles</c>
/// (synced by #13), and applies <see cref="TrackingRule"/>.
/// </summary>
internal sealed class TrackingStateLoader(ApolloDbContext db)
{
    /// <summary>
    /// The tracking role setting of the guild on <paramref name="platform"/>, with the role's name (for the log) and its
    /// platform role (for the tracking commands); both <see langword="null"/> without a tracking role.
    /// </summary>
    public async Task<(TrackingSettings Settings, string? RoleName, PlatformRef? Role)> LoadSettingsAsync(
        Guid guildId,
        PlatformKind platform,
        CancellationToken cancellationToken)
    {
        var trackingRole = await db.RoleSettings.AsNoTracking()
            .Where(s => s.GuildId == guildId && s.Platform == platform && s.Purpose == RoleSettingPurpose.Tracking)
            .Join(db.Roles, s => s.RoleId, r => r.Id, (s, r) => r)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.DeletedAt,
                ExternalId = db.RolePlatformIds.Where(p => p.RoleId == r.Id && p.Platform == platform).Select(p => p.ExternalId).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return (
            new TrackingSettings(trackingRole?.Id, trackingRole?.DeletedAt is not null),
            trackingRole?.Name,
            trackingRole?.ExternalId is { } externalId ? new PlatformRef(platform, externalId) : null);
    }

    /// <summary>
    /// The tracked users among the guild's members (only <paramref name="userId"/> when given), with their user on
    /// <paramref name="platform"/>. A member without a user on that platform is not tracked by this bot.
    /// </summary>
    public async Task<Dictionary<Guid, PlatformRef>> LoadTrackedAsync(
        Guid guildId,
        PlatformKind platform,
        TrackingSettings settings,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        // Members who left are never tracked, so only current members are loaded.
        var members = await db.GuildMembers.AsNoTracking()
            .Where(m => m.GuildId == guildId && m.LeftAt == null && (userId == null || m.UserId == userId))
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);
        var roles = (await db.MemberRoles.AsNoTracking()
                .Where(r => r.GuildId == guildId && (userId == null || r.UserId == userId))
                .Select(r => new { r.UserId, r.RoleId })
                .ToListAsync(cancellationToken))
            .ToLookup(r => r.UserId, r => r.RoleId);

        var tracked = members
            .Where(userId => TrackingRule.IsTracked(settings, new TrackingMember(IsCurrentMember: true, roles[userId].ToHashSet())))
            .ToList();

        return await db.UserPlatformIds.AsNoTracking()
            .Where(u => u.Platform == platform && tracked.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => new PlatformRef(u.Platform, u.ExternalId), cancellationToken);
    }

    /// <summary>
    /// Makes <c>tracking_periods</c> match the tracked set (for <paramref name="userId"/> only when given): closes the
    /// open period of every user who is no longer tracked and opens one for every tracked user without one.
    /// </summary>
    public async Task SyncPeriodsAsync(
        Guid guildId,
        IReadOnlyCollection<Guid> tracked,
        Guid? userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var open = await db.TrackingPeriods
            .Where(p => p.GuildId == guildId && p.To == null && (userId == null || p.UserId == userId))
            .ToListAsync(cancellationToken);

        foreach (var period in open.Where(p => !tracked.Contains(p.UserId)))
        {
            period.To = now;
        }

        var withOpenPeriod = open.Select(p => p.UserId).ToHashSet();
        db.TrackingPeriods.AddRange(tracked
            .Where(id => (userId == null || id == userId) && !withOpenPeriod.Contains(id))
            .Select(id => new TrackingPeriod { GuildId = guildId, UserId = id, From = now }));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
