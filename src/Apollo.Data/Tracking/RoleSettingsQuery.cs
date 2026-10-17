using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Tracking;

/// <summary>
/// Reads what the admin settings page shows (#33, scope §5.1.2, §7.2) from the synced guild state (#13), the role
/// settings and their audit log (#14). Member counts are current, non-bot members (<c>member_roles</c> of members with
/// <c>left_at IS NULL</c>).
/// </summary>
public sealed class RoleSettingsQuery(ApolloDbContext db)
{
    /// <summary>
    /// The internal ID of <paramref name="guild"/> once the guild state sync has applied a snapshot of it (it names the
    /// guild); <see langword="null"/> before, so a platform without synced data has no settings.
    /// </summary>
    public async Task<Guid?> FindSyncedGuildAsync(PlatformRef guild, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(guild);
        return await db.GuildPlatformIds.AsNoTracking()
            .Where(m => m.Platform == guild.Platform && m.ExternalId == guild.ExternalId)
            .Join(db.Guilds.Where(g => g.Name != null), m => m.GuildId, g => g.Id, (m, g) => (Guid?)g.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<RoleSettingsOverview> GetAsync(Guid guildId, PlatformKind platform, CancellationToken cancellationToken)
    {
        var memberCounts = await CurrentHumanMemberRoles(guildId)
            .GroupBy(r => r.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.RoleId, g => g.Count, cancellationToken);

        var roles = (await PlatformRoles(guildId, platform)
                .OrderByDescending(r => r.Position)
                .ThenBy(r => r.Name)
                .ToListAsync(cancellationToken))
            .Select(r => new SettingsRole(r.Id, r.Name, r.Color, r.Position, r.DeletedAt is not null, memberCounts.GetValueOrDefault(r.Id)))
            .ToList();

        var settings = await db.RoleSettings.AsNoTracking()
            .Where(s => s.GuildId == guildId && s.Platform == platform)
            .Select(s => new { s.Purpose, s.RoleId })
            .ToListAsync(cancellationToken);
        var trackingRoleId = settings.Where(s => s.Purpose == RoleSettingPurpose.Tracking).Select(s => (Guid?)s.RoleId).SingleOrDefault();

        return new RoleSettingsOverview(
            roles,
            trackingRoleId,
            [.. settings.Where(s => s.Purpose == RoleSettingPurpose.MemberAccess).Select(s => s.RoleId)],
            TrackingRule.ModeOf(await LoadTrackingSettingsAsync(guildId, platform, trackingRoleId, cancellationToken)),
            await LoadBotFactsAsync(guildId, platform, cancellationToken));
    }

    /// <summary>
    /// How many members hold at least one of <paramref name="roleIds"/>, and who would start or stop being tracked if
    /// they were the tracking role. Deleted roles and roles of other guilds or platforms are held by nobody.
    /// </summary>
    public async Task<RolePreview> PreviewAsync(Guid guildId, PlatformKind platform, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roleIds);

        var selectable = PlatformRoles(guildId, platform).Where(r => r.DeletedAt == null && roleIds.Contains(r.Id)).Select(r => r.Id);
        var holders = (await CurrentHumanMemberRoles(guildId)
                .Where(r => selectable.Contains(r.RoleId))
                .Select(r => r.UserId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var trackingRoleId = await db.RoleSettings.AsNoTracking()
            .Where(s => s.GuildId == guildId && s.Platform == platform && s.Purpose == RoleSettingPurpose.Tracking)
            .Select(s => (Guid?)s.RoleId)
            .SingleOrDefaultAsync(cancellationToken);
        var trackingSettings = await LoadTrackingSettingsAsync(guildId, platform, trackingRoleId, cancellationToken);
        var tracked = await LoadTrackedAsync(guildId, trackingSettings, cancellationToken);

        return new RolePreview(holders.Count, holders.Count(id => !tracked.Contains(id)), tracked.Count(id => !holders.Contains(id)));
    }

    /// <summary>The <paramref name="take"/> newest changes, newest first.</summary>
    public async Task<IReadOnlyList<RoleSettingHistoryEntry>> GetHistoryAsync(Guid guildId, PlatformKind platform, int take, CancellationToken cancellationToken) =>
        await (
                from change in db.RoleSettingChanges.AsNoTracking()
                where change.GuildId == guildId && change.Platform == platform
                join role in db.Roles on change.RoleId equals role.Id
                join author in db.Users on change.ChangedByUserId equals author.Id into authors
                from author in authors.DefaultIfEmpty()
                orderby change.ChangedAt descending, change.Id descending
                select new RoleSettingHistoryEntry(change.ChangedAt, author.DisplayName, change.Purpose, change.Action, change.RoleId, role.Name))
            .Take(take)
            .ToListAsync(cancellationToken);

    private IQueryable<Identity.Role> PlatformRoles(Guid guildId, PlatformKind platform) =>
        db.Roles.AsNoTracking()
            .Where(r => r.GuildId == guildId && db.RolePlatformIds.Any(p => p.RoleId == r.Id && p.Platform == platform));

    private IQueryable<Guilds.MemberRole> CurrentHumanMemberRoles(Guid guildId) =>
        db.MemberRoles.AsNoTracking()
            .Where(r => r.GuildId == guildId
                && db.GuildMembers.Any(m => m.GuildId == guildId && m.UserId == r.UserId && m.LeftAt == null && !m.IsBot));

    private async Task<TrackingSettings> LoadTrackingSettingsAsync(Guid guildId, PlatformKind platform, Guid? trackingRoleId, CancellationToken cancellationToken)
    {
        var deleted = trackingRoleId is { } roleId
            && await db.Roles.AnyAsync(r => r.Id == roleId && r.DeletedAt != null, cancellationToken);

        // TEMPORARY: removed by #62, like the same check in the bot's TrackingStateLoader.
        var everConfigured = await db.RoleSettingChanges.AsNoTracking().AnyAsync(
            c => c.GuildId == guildId && c.Platform == platform && c.Purpose == RoleSettingPurpose.Tracking && c.Action == RoleSettingAction.Added,
            cancellationToken);

        return new TrackingSettings(trackingRoleId, deleted, everConfigured);
    }

    /// <summary>The current, non-bot members <see cref="TrackingRule"/> tracks with <paramref name="settings"/>.</summary>
    private async Task<HashSet<Guid>> LoadTrackedAsync(Guid guildId, TrackingSettings settings, CancellationToken cancellationToken)
    {
        var members = await db.GuildMembers.AsNoTracking()
            .Where(m => m.GuildId == guildId && m.LeftAt == null && !m.IsBot)
            .Select(m => new { m.UserId, m.IsAdministrator })
            .ToListAsync(cancellationToken);
        var roles = (await CurrentHumanMemberRoles(guildId).Select(r => new { r.UserId, r.RoleId }).ToListAsync(cancellationToken))
            .ToLookup(r => r.UserId, r => r.RoleId);

        return members
            .Where(m => TrackingRule.IsTracked(settings, new TrackingMember(IsCurrentMember: true, m.IsAdministrator, roles[m.UserId].ToHashSet())))
            .Select(m => m.UserId)
            .ToHashSet();
    }

    private async Task<BotRoleFacts?> LoadBotFactsAsync(Guid guildId, PlatformKind platform, CancellationToken cancellationToken)
    {
        var bot = await (
                from mapping in db.GuildPlatformIds.AsNoTracking()
                where mapping.GuildId == guildId && mapping.Platform == platform && mapping.BotUserId != null
                join member in db.GuildMembers on new { GuildId = guildId, UserId = mapping.BotUserId!.Value } equals new { member.GuildId, member.UserId }
                where member.LeftAt == null
                select new { member.UserId, member.IsAdministrator })
            .SingleOrDefaultAsync(cancellationToken);
        if (bot is null)
        {
            return null;
        }

        var held = await db.MemberRoles.AsNoTracking()
            .Where(r => r.GuildId == guildId && r.UserId == bot.UserId)
            .Join(db.Roles.Where(r => r.DeletedAt == null), r => r.RoleId, role => role.Id, (_, role) => new { role.Position, role.ManagesRoles })
            .ToListAsync(cancellationToken);

        // The Administrator flag already covers Administrator roles and the guild owner (MemberAdministrator).
        return new BotRoleFacts(
            bot.IsAdministrator || held.Any(r => r.ManagesRoles),
            held.Count == 0 ? null : held.Max(r => r.Position));
    }
}
