using Apollo.Data;
using Apollo.Data.Guilds;
using Apollo.Data.Identity;
using Apollo.Domain.Guilds;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Bot.GuildState;

/// <summary>
/// Mirrors the guild's channels, roles, members, member roles and Administrator flags into the database (scope §5.1,
/// §6.2), so the tracking gate, the role settings and the web access rules read guild state from the DB instead of the
/// platform. A snapshot (<see cref="PlatformGuildAvailable"/>, on startup and after every reconnect) is reconciled in
/// full; live events keep the state current in between. Deleted roles and channels and departed members keep their
/// rows, marked with <c>deleted_at</c> / <c>left_at</c>.
/// </summary>
/// <remarks>
/// <para>
/// After a committed change it publishes <see cref="MemberAccessStateChanged"/> for every member whose membership,
/// roles or Administrator flag changed, and <see cref="GuildStateSynchronized"/> after each snapshot.
/// </para>
/// <para>
/// Every handler resolves its platform IDs before it opens a transaction: <see cref="PlatformIdCache"/> keeps what
/// it resolved, so an ID created inside a transaction that rolls back would stay cached without a row.
/// </para>
/// </remarks>
internal sealed partial class GuildStateSync(
    ApolloDbContext db,
    IPlatformIdResolver ids,
    IChatPlatform platform,
    GuildStateSyncLock syncLock,
    GuildStateNotificationPublisher publisher,
    ILogger<GuildStateSync> logger)
    : IPlatformEventHandler<PlatformGuildAvailable>,
        IPlatformEventHandler<PlatformMemberJoined>,
        IPlatformEventHandler<PlatformMemberUpdated>,
        IPlatformEventHandler<PlatformMemberLeft>,
        IPlatformEventHandler<PlatformRoleCreated>,
        IPlatformEventHandler<PlatformRoleUpdated>,
        IPlatformEventHandler<PlatformRoleDeleted>,
        IPlatformEventHandler<PlatformChannelCreated>,
        IPlatformEventHandler<PlatformChannelUpdated>,
        IPlatformEventHandler<PlatformChannelDeleted>
{
    public async Task HandleAsync(PlatformGuildAvailable platformEvent, CancellationToken cancellationToken)
    {
        using var _ = await syncLock.AcquireAsync(cancellationToken);

        // The snapshot's member list may be partial (large guilds, Fluxer): always ask for the full list first
        // (docs/platform-parity.md). Under the lock, so live member events wait and apply on top of it.
        var members = await platform.ListMembersAsync(platformEvent.Guild, cancellationToken);

        var guildId = await ids.ResolveGuildAsync(platformEvent.Guild, cancellationToken);
        var ownerId = await ids.ResolveUserAsync(platformEvent.Owner, cancellationToken);
        await ResolveRolesAsync(platformEvent.Guild, platformEvent.Roles.Select(r => r.Role), cancellationToken);
        foreach (var channel in platformEvent.Channels)
        {
            await ResolveChannelAsync(platformEvent.Guild, channel, cancellationToken);
        }

        foreach (var member in members)
        {
            await ResolveMemberAsync(platformEvent.Guild, member, cancellationToken);
        }

        var before = await LoadAccessStatesAsync(guildId, userIds: null, cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var guild = await db.Guilds.SingleAsync(g => g.Id == guildId, cancellationToken);
            guild.Name = platformEvent.Name;
            guild.OwnerUserId = ownerId;

            var roleIds = new HashSet<Guid>();
            foreach (var role in platformEvent.Roles)
            {
                roleIds.Add(await ApplyRoleAsync(platformEvent.Guild, role, cancellationToken));
            }

            var deletedRoles = await db.Roles
                .Where(r => r.GuildId == guildId && r.DeletedAt == null && !roleIds.Contains(r.Id))
                .ToListAsync(cancellationToken);
            await MarkRolesDeletedAsync(guildId, deletedRoles, platformEvent.OccurredAt, cancellationToken);

            var channelIds = new HashSet<Guid>();
            foreach (var channel in platformEvent.Channels)
            {
                channelIds.Add(await ApplyChannelAsync(platformEvent.Guild, channel, cancellationToken));
            }

            // A snapshot lists only active threads, and an archived thread still exists: only a delete event marks one.
            var deletedChannels = await db.Channels
                .Where(c => c.GuildId == guildId && c.DeletedAt == null && c.Type != PlatformChannelType.Thread && !channelIds.Contains(c.Id))
                .ToListAsync(cancellationToken);
            foreach (var channel in deletedChannels)
            {
                channel.DeletedAt = platformEvent.OccurredAt;
            }

            var userIds = new HashSet<Guid>();
            foreach (var member in members)
            {
                userIds.Add(await ApplyMemberAsync(platformEvent.Guild, guildId, member, cancellationToken));
            }

            var departed = await db.GuildMembers
                .Where(m => m.GuildId == guildId && m.LeftAt == null && !userIds.Contains(m.UserId))
                .Select(m => m.UserId)
                .ToListAsync(cancellationToken);
            foreach (var userId in departed)
            {
                await MarkLeftAsync(guildId, userId, platformEvent.OccurredAt, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            await RecomputeAdministratorsAsync(guildId, userIds: null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            LogSynchronized(platformEvent.Guild, roleIds.Count, channelIds.Count, userIds.Count, departed.Count, deletedRoles.Count, deletedChannels.Count);
        }

        await PublishMemberChangesAsync(guildId, before, userIds: null, cancellationToken);
        await publisher.PublishAsync(new GuildStateSynchronized(guildId), cancellationToken);
    }

    public Task HandleAsync(PlatformMemberJoined platformEvent, CancellationToken cancellationToken) =>
        ApplyMemberEventAsync(platformEvent.Guild, platformEvent.Member, cancellationToken);

    public Task HandleAsync(PlatformMemberUpdated platformEvent, CancellationToken cancellationToken) =>
        ApplyMemberEventAsync(platformEvent.Guild, platformEvent.Member, cancellationToken);

    public async Task HandleAsync(PlatformMemberLeft platformEvent, CancellationToken cancellationToken)
    {
        using var _ = await syncLock.AcquireAsync(cancellationToken);
        if (await ids.TryGetUserAsync(platformEvent.User, cancellationToken) is not { } userId)
        {
            return;
        }

        var guildId = await ids.ResolveGuildAsync(platformEvent.Guild, cancellationToken);
        var before = await LoadAccessStatesAsync(guildId, [userId], cancellationToken);
        await MarkLeftAsync(guildId, userId, platformEvent.OccurredAt, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await PublishMemberChangesAsync(guildId, before, [userId], cancellationToken);
    }

    public Task HandleAsync(PlatformRoleCreated platformEvent, CancellationToken cancellationToken) =>
        ApplyRoleEventAsync(platformEvent.Guild, platformEvent.Role, cancellationToken);

    public Task HandleAsync(PlatformRoleUpdated platformEvent, CancellationToken cancellationToken) =>
        ApplyRoleEventAsync(platformEvent.Guild, platformEvent.Role, cancellationToken);

    public async Task HandleAsync(PlatformRoleDeleted platformEvent, CancellationToken cancellationToken)
    {
        using var _ = await syncLock.AcquireAsync(cancellationToken);
        if (await ids.TryGetRoleAsync(platformEvent.Role, cancellationToken) is not { } roleId)
        {
            return;
        }

        var role = await db.Roles.SingleAsync(r => r.Id == roleId, cancellationToken);
        var holders = await HoldersAsync(roleId, cancellationToken);
        var before = await LoadAccessStatesAsync(role.GuildId, holders, cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await MarkRolesDeletedAsync(role.GuildId, [role], platformEvent.OccurredAt, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await RecomputeAdministratorsAsync(role.GuildId, holders, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await PublishMemberChangesAsync(role.GuildId, before, holders, cancellationToken);
    }

    public Task HandleAsync(PlatformChannelCreated platformEvent, CancellationToken cancellationToken) =>
        ApplyChannelEventAsync(platformEvent.Guild, platformEvent.Channel, cancellationToken);

    public Task HandleAsync(PlatformChannelUpdated platformEvent, CancellationToken cancellationToken) =>
        ApplyChannelEventAsync(platformEvent.Guild, platformEvent.Channel, cancellationToken);

    public async Task HandleAsync(PlatformChannelDeleted platformEvent, CancellationToken cancellationToken)
    {
        using var _ = await syncLock.AcquireAsync(cancellationToken);
        if (await ids.TryGetChannelAsync(platformEvent.Channel, cancellationToken) is not { } channelId)
        {
            return;
        }

        var channel = await db.Channels.SingleAsync(c => c.Id == channelId, cancellationToken);
        channel.DeletedAt ??= platformEvent.OccurredAt;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyMemberEventAsync(PlatformRef guild, PlatformMember member, CancellationToken cancellationToken)
    {
        using var _ = await syncLock.AcquireAsync(cancellationToken);
        var guildId = await ids.ResolveGuildAsync(guild, cancellationToken);
        var userId = await ResolveMemberAsync(guild, member, cancellationToken);

        var before = await LoadAccessStatesAsync(guildId, [userId], cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await ApplyMemberAsync(guild, guildId, member, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await RecomputeAdministratorsAsync(guildId, [userId], cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await PublishMemberChangesAsync(guildId, before, [userId], cancellationToken);
    }

    private async Task ApplyRoleEventAsync(PlatformRef guild, PlatformRole role, CancellationToken cancellationToken)
    {
        using var _ = await syncLock.AcquireAsync(cancellationToken);
        var guildId = await ids.ResolveGuildAsync(guild, cancellationToken);
        var roleId = await ids.ResolveRoleAsync(guild, role.Role, cancellationToken);

        var holders = await HoldersAsync(roleId, cancellationToken);
        var before = await LoadAccessStatesAsync(guildId, holders, cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await ApplyRoleAsync(guild, role, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await RecomputeAdministratorsAsync(guildId, holders, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await PublishMemberChangesAsync(guildId, before, holders, cancellationToken);
    }

    private async Task ApplyChannelEventAsync(PlatformRef guild, PlatformChannel channel, CancellationToken cancellationToken)
    {
        using var _ = await syncLock.AcquireAsync(cancellationToken);
        await ApplyChannelAsync(guild, channel, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ResolveRolesAsync(PlatformRef guild, IEnumerable<PlatformRef> roles, CancellationToken cancellationToken)
    {
        foreach (var role in roles)
        {
            await ids.ResolveRoleAsync(guild, role, cancellationToken);
        }
    }

    private async Task ResolveChannelAsync(PlatformRef guild, PlatformChannel channel, CancellationToken cancellationToken)
    {
        foreach (var channelRef in new[] { channel.Channel, channel.Category, channel.ThreadParent }.OfType<PlatformRef>())
        {
            await ids.ResolveChannelAsync(guild, channelRef, cancellationToken);
        }
    }

    private async Task<Guid> ResolveMemberAsync(PlatformRef guild, PlatformMember member, CancellationToken cancellationToken)
    {
        await ResolveRolesAsync(guild, member.Roles, cancellationToken);
        return await ids.ResolveUserAsync(member.User, cancellationToken);
    }

    private async Task<Guid> ApplyRoleAsync(PlatformRef guild, PlatformRole snapshot, CancellationToken cancellationToken)
    {
        var roleId = await ids.ResolveRoleAsync(guild, snapshot.Role, cancellationToken);
        var role = await db.Roles.SingleAsync(r => r.Id == roleId, cancellationToken);
        role.Name = snapshot.Name;
        role.Color = snapshot.Color;
        role.Position = snapshot.Position;
        role.IsAdministrator = snapshot.IsAdministrator;
        role.DeletedAt = null;
        return roleId;
    }

    private async Task<Guid> ApplyChannelAsync(PlatformRef guild, PlatformChannel snapshot, CancellationToken cancellationToken)
    {
        var channelId = await ids.ResolveChannelAsync(guild, snapshot.Channel, cancellationToken);
        var categoryId = snapshot.Category is { } category ? await ids.ResolveChannelAsync(guild, category, cancellationToken) : (Guid?)null;
        var threadParentId = snapshot.ThreadParent is { } parent ? await ids.ResolveChannelAsync(guild, parent, cancellationToken) : (Guid?)null;

        var channel = await db.Channels.SingleAsync(c => c.Id == channelId, cancellationToken);
        channel.Name = snapshot.Name;
        channel.Type = snapshot.Type;
        channel.CategoryId = categoryId;
        channel.ThreadParentId = threadParentId;
        channel.DeletedAt = null;
        return channelId;
    }

    /// <summary>
    /// Stores the member's user columns, membership and exact role list. The Administrator flag follows from the saved
    /// roles in <see cref="RecomputeAdministratorsAsync"/>.
    /// </summary>
    private async Task<Guid> ApplyMemberAsync(PlatformRef guild, Guid guildId, PlatformMember snapshot, CancellationToken cancellationToken)
    {
        var userId = await ids.ResolveUserAsync(snapshot.User, cancellationToken);
        var user = await db.Users.SingleAsync(u => u.Id == userId, cancellationToken);
        user.DisplayName = snapshot.DisplayName;
        user.AvatarUrl = snapshot.AvatarUrl;

        var member = await db.GuildMembers.SingleOrDefaultAsync(m => m.GuildId == guildId && m.UserId == userId, cancellationToken);
        if (member is null)
        {
            member = new GuildMember { GuildId = guildId, UserId = userId };
            db.GuildMembers.Add(member);
        }

        member.JoinedAt = snapshot.JoinedAt;
        member.IsBot = snapshot.IsBot;
        member.LeftAt = null;

        var roleIds = new HashSet<Guid>();
        foreach (var role in snapshot.Roles)
        {
            roleIds.Add(await ids.ResolveRoleAsync(guild, role, cancellationToken));
        }

        var current = await db.MemberRoles.Where(r => r.GuildId == guildId && r.UserId == userId).ToListAsync(cancellationToken);
        db.MemberRoles.RemoveRange(current.Where(r => !roleIds.Contains(r.RoleId)));
        db.MemberRoles.AddRange(roleIds
            .Except(current.Select(r => r.RoleId))
            .Select(roleId => new MemberRole { GuildId = guildId, UserId = userId, RoleId = roleId }));
        return userId;
    }

    private async Task MarkLeftAsync(Guid guildId, Guid userId, DateTimeOffset leftAt, CancellationToken cancellationToken)
    {
        if (await db.GuildMembers.SingleOrDefaultAsync(m => m.GuildId == guildId && m.UserId == userId, cancellationToken) is not { } member)
        {
            return;
        }

        member.LeftAt ??= leftAt;
        member.IsAdministrator = false;
        db.MemberRoles.RemoveRange(await db.MemberRoles.Where(r => r.GuildId == guildId && r.UserId == userId).ToListAsync(cancellationToken));
    }

    private async Task MarkRolesDeletedAsync(Guid guildId, IReadOnlyCollection<Role> roles, DateTimeOffset deletedAt, CancellationToken cancellationToken)
    {
        if (roles.Count == 0)
        {
            return;
        }

        foreach (var role in roles)
        {
            role.DeletedAt ??= deletedAt;
        }

        var roleIds = roles.Select(r => r.Id).ToList();
        db.MemberRoles.RemoveRange(await db.MemberRoles
            .Where(r => r.GuildId == guildId && roleIds.Contains(r.RoleId))
            .ToListAsync(cancellationToken));
    }

    private Task<List<Guid>> HoldersAsync(Guid roleId, CancellationToken cancellationToken) =>
        db.MemberRoles.Where(r => r.RoleId == roleId).Select(r => r.UserId).ToListAsync(cancellationToken);

    /// <summary>
    /// Sets <see cref="GuildMember.IsAdministrator"/> from the saved roles and the guild owner, for the given current
    /// members (all of them when <paramref name="userIds"/> is <see langword="null"/>).
    /// </summary>
    private async Task RecomputeAdministratorsAsync(Guid guildId, IReadOnlyCollection<Guid>? userIds, CancellationToken cancellationToken)
    {
        var ownerId = await db.Guilds.Where(g => g.Id == guildId).Select(g => g.OwnerUserId).SingleAsync(cancellationToken);
        var members = await db.GuildMembers
            .Where(m => m.GuildId == guildId && m.LeftAt == null && (userIds == null || userIds.Contains(m.UserId)))
            .ToListAsync(cancellationToken);
        var heldRoles = (await db.MemberRoles
                .Where(r => r.GuildId == guildId && (userIds == null || userIds.Contains(r.UserId)))
                .Join(db.Roles, memberRole => memberRole.RoleId, role => role.Id, (memberRole, role) => new { memberRole.UserId, role.IsAdministrator })
                .ToListAsync(cancellationToken))
            .ToLookup(r => r.UserId, r => r.IsAdministrator);

        foreach (var member in members)
        {
            member.IsAdministrator = MemberAdministrator.IsAdministrator(member.UserId == ownerId, heldRoles[member.UserId]);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// What a member's access depends on, so <see cref="MemberAccessStateChanged"/> is published only for real changes.
    /// </summary>
    private async Task<Dictionary<Guid, AccessState>> LoadAccessStatesAsync(Guid guildId, IReadOnlyCollection<Guid>? userIds, CancellationToken cancellationToken)
    {
        var members = await db.GuildMembers.AsNoTracking()
            .Where(m => m.GuildId == guildId && (userIds == null || userIds.Contains(m.UserId)))
            .ToListAsync(cancellationToken);
        var roles = (await db.MemberRoles.AsNoTracking()
                .Where(r => r.GuildId == guildId && (userIds == null || userIds.Contains(r.UserId)))
                .ToListAsync(cancellationToken))
            .ToLookup(r => r.UserId, r => r.RoleId);

        return members.ToDictionary(
            m => m.UserId,
            m => new AccessState(m.LeftAt == null, m.IsAdministrator, string.Join(',', roles[m.UserId].Order())));
    }

    private async Task PublishMemberChangesAsync(
        Guid guildId,
        Dictionary<Guid, AccessState> before,
        IReadOnlyCollection<Guid>? userIds,
        CancellationToken cancellationToken)
    {
        var after = await LoadAccessStatesAsync(guildId, userIds, cancellationToken);
        foreach (var (userId, state) in after)
        {
            if (!before.TryGetValue(userId, out var previous) || previous != state)
            {
                await publisher.PublishAsync(new MemberAccessStateChanged(guildId, userId), cancellationToken);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Synchronized guild {Guild}: {RoleCount} roles, {ChannelCount} channels, {MemberCount} members; marked {LeftCount} members left, {DeletedRoleCount} roles and {DeletedChannelCount} channels deleted")]
    private partial void LogSynchronized(PlatformRef guild, int roleCount, int channelCount, int memberCount, int leftCount, int deletedRoleCount, int deletedChannelCount);

    private readonly record struct AccessState(bool IsMember, bool IsAdministrator, string Roles);
}
