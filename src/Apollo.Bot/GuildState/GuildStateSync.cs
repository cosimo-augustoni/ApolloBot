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
/// A snapshot runs in two passes. The structure pass applies the guild, roles and channels from the event. The member
/// pass applies the full member list from <see cref="IChatPlatform.ListMembersAsync"/>; members missing from it are
/// marked left. An empty or failed list is never taken as "everyone left": the member pass is retried
/// (<see cref="MemberListAttempts"/>, <see cref="MemberListRetryDelays"/>) and otherwise skipped until the next snapshot.
/// A newer snapshot or the bot stopping ends the retries (<see cref="GuildSnapshotRuns"/>).
/// </para>
/// <para>
/// Live events are serialised but not ordered, so an update may arrive after the delete or leave it predates. Updates
/// therefore never clear <c>deleted_at</c> / <c>left_at</c>; only a create, a join or a snapshot does.
/// </para>
/// <para>
/// After a committed change it publishes <see cref="MemberAccessStateChanged"/> for every member whose membership,
/// roles or Administrator flag changed, and <see cref="GuildStateSynchronized"/> once a snapshot's member pass is done.
/// </para>
/// <para>
/// Every pass resolves its platform IDs before it opens a transaction: <see cref="PlatformIdCache"/> keeps what it
/// resolved, so an ID created inside a transaction that rolls back would stay cached without a row.
/// </para>
/// </remarks>
internal sealed partial class GuildStateSync(
    ApolloDbContext db,
    IPlatformIdResolver ids,
    IChatPlatform platform,
    GuildStateSyncLock syncLock,
    GuildSnapshotRuns snapshotRuns,
    GuildStateNotificationPublisher publisher,
    TimeProvider timeProvider,
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
    /// <summary>
    /// How often a snapshot asks for the full member list before it gives up on members until the next snapshot.
    /// </summary>
    public const int MemberListAttempts = 3;

    /// <summary>
    /// The pause before each retry of the member list. At least 30 s: Fluxer allows one full request per 30 s per guild.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> MemberListRetryDelays = [TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)];

    public async Task HandleAsync(PlatformGuildAvailable platformEvent, CancellationToken cancellationToken)
    {
        var (guildId, run) = await ApplyStructureAsync(platformEvent, cancellationToken);
        using (run)
        {
            try
            {
                // The lock is held per attempt only, so live events apply between the retries.
                for (var attempt = 1; attempt <= MemberListAttempts; attempt++)
                {
                    if (attempt > 1)
                    {
                        await Task.Delay(MemberListRetryDelays[attempt - 2], timeProvider, run.Token);
                    }

                    if (await TryApplyMembersAsync(platformEvent, guildId, attempt, run.Token))
                    {
                        await publisher.PublishAsync(new GuildStateSynchronized(guildId), cancellationToken);
                        return;
                    }
                }

                LogMembersNotSynchronized(platformEvent.Guild, MemberListAttempts);
            }
            catch (OperationCanceledException) when (run.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // A newer snapshot took over, or the bot is stopping.
                LogMemberPassStopped(platformEvent.Guild);
            }
        }
    }

    public Task HandleAsync(PlatformMemberJoined platformEvent, CancellationToken cancellationToken) =>
        ApplyMemberEventAsync(platformEvent.Guild, platformEvent.Member, revive: true, cancellationToken);

    public Task HandleAsync(PlatformMemberUpdated platformEvent, CancellationToken cancellationToken) =>
        ApplyMemberEventAsync(platformEvent.Guild, platformEvent.Member, revive: false, cancellationToken);

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
        ApplyRoleEventAsync(platformEvent.Guild, platformEvent.Role, revive: true, cancellationToken);

    public Task HandleAsync(PlatformRoleUpdated platformEvent, CancellationToken cancellationToken) =>
        ApplyRoleEventAsync(platformEvent.Guild, platformEvent.Role, revive: false, cancellationToken);

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
        ApplyChannelEventAsync(platformEvent.Guild, platformEvent.Channel, revive: true, cancellationToken);

    public Task HandleAsync(PlatformChannelUpdated platformEvent, CancellationToken cancellationToken) =>
        ApplyChannelEventAsync(platformEvent.Guild, platformEvent.Channel, revive: false, cancellationToken);

    public async Task HandleAsync(PlatformChannelDeleted platformEvent, CancellationToken cancellationToken)
    {
        using var _ = await syncLock.AcquireAsync(cancellationToken);
        if (await ids.TryGetChannelAsync(platformEvent.Channel, cancellationToken) is not { } channelId)
        {
            return;
        }

        var channel = await db.Channels.SingleAsync(c => c.Id == channelId, cancellationToken);
        await MarkChannelsDeletedAsync([channel], platformEvent.OccurredAt, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The snapshot's guild, roles and channels; roles and channels it no longer lists are marked deleted. Also starts
    /// the snapshot's run (under the lock, so runs start in the order the snapshots are applied), which cancels the
    /// member retries of an older snapshot.
    /// </summary>
    private async Task<(Guid GuildId, CancellationTokenSource Run)> ApplyStructureAsync(PlatformGuildAvailable snapshot, CancellationToken cancellationToken)
    {
        using var _ = await syncLock.AcquireAsync(cancellationToken);
        var guildId = await ids.ResolveGuildAsync(snapshot.Guild, cancellationToken);
        var ownerId = await ids.ResolveUserAsync(snapshot.Owner, cancellationToken);
        var botId = await ids.ResolveUserAsync(snapshot.Bot, cancellationToken);
        await ResolveRolesAsync(snapshot.Guild, snapshot.Roles.Select(r => r.Role), cancellationToken);
        foreach (var channel in snapshot.Channels)
        {
            await ResolveChannelAsync(snapshot.Guild, channel, cancellationToken);
        }

        var before = await LoadAccessStatesAsync(guildId, userIds: null, cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var guild = await db.Guilds.SingleAsync(g => g.Id == guildId, cancellationToken);
            guild.Name = snapshot.Name;
            guild.OwnerUserId = ownerId;

            // Per platform mapping: a guild linked on several platforms has one bot user on each (admin page, #33).
            var mapping = await db.GuildPlatformIds.SingleAsync(
                m => m.Platform == snapshot.Guild.Platform && m.ExternalId == snapshot.Guild.ExternalId,
                cancellationToken);
            mapping.BotUserId = botId;

            var roleIds = new HashSet<Guid>();
            foreach (var role in snapshot.Roles)
            {
                roleIds.Add(await ApplyRoleAsync(snapshot.Guild, role, revive: true, cancellationToken));
            }

            var deletedRoles = await db.Roles
                .Where(r => r.GuildId == guildId && r.DeletedAt == null && !roleIds.Contains(r.Id))
                .ToListAsync(cancellationToken);
            await MarkRolesDeletedAsync(guildId, deletedRoles, snapshot.OccurredAt, cancellationToken);

            var channelIds = new HashSet<Guid>();
            foreach (var channel in snapshot.Channels)
            {
                channelIds.Add(await ApplyChannelAsync(snapshot.Guild, channel, revive: true, cancellationToken));
            }

            // A snapshot lists only active threads, and an archived thread still exists: a missing thread is marked
            // deleted only together with its parent channel.
            var deletedChannels = await db.Channels
                .Where(c => c.GuildId == guildId && c.DeletedAt == null && c.Type != PlatformChannelType.Thread && !channelIds.Contains(c.Id))
                .ToListAsync(cancellationToken);
            await MarkChannelsDeletedAsync(deletedChannels, snapshot.OccurredAt, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            await RecomputeAdministratorsAsync(guildId, userIds: null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            LogStructureSynchronized(snapshot.Guild, roleIds.Count, channelIds.Count, deletedRoles.Count, deletedChannels.Count);
        }

        await PublishMemberChangesAsync(guildId, before, userIds: null, cancellationToken);
        return (guildId, snapshotRuns.Start(cancellationToken));
    }

    /// <summary>
    /// The member pass of a snapshot. <see langword="false"/> when the full member list is unavailable (empty or failed);
    /// nothing is changed then.
    /// </summary>
    private async Task<bool> TryApplyMembersAsync(PlatformGuildAvailable snapshot, Guid guildId, int attempt, CancellationToken cancellationToken)
    {
        using var _ = await syncLock.AcquireAsync(cancellationToken);

        // The snapshot's own member list may be partial (large guilds, Fluxer): only the full list may mark members as
        // left (docs/platform-parity.md).
        IReadOnlyList<PlatformMember> members;
        try
        {
            members = await platform.ListMembersAsync(snapshot.Guild, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogMemberListFailed(exception, snapshot.Guild, attempt, MemberListAttempts);
            return false;
        }

        // Every guild has at least the bot as a member, so an empty list means the platform did not deliver one.
        if (members.Count == 0)
        {
            LogMemberListEmpty(snapshot.Guild, attempt, MemberListAttempts);
            return false;
        }

        foreach (var member in members)
        {
            await ResolveMemberAsync(snapshot.Guild, member, cancellationToken);
        }

        var before = await LoadAccessStatesAsync(guildId, userIds: null, cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var userIds = new HashSet<Guid>();
            foreach (var member in members)
            {
                userIds.Add(await ApplyMemberAsync(snapshot.Guild, guildId, member, revive: true, cancellationToken));
            }

            var departed = await db.GuildMembers
                .Where(m => m.GuildId == guildId && m.LeftAt == null && !userIds.Contains(m.UserId))
                .Select(m => m.UserId)
                .ToListAsync(cancellationToken);
            foreach (var userId in departed)
            {
                await MarkLeftAsync(guildId, userId, snapshot.OccurredAt, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            await RecomputeAdministratorsAsync(guildId, userIds: null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            LogMembersSynchronized(snapshot.Guild, userIds.Count, departed.Count);
        }

        await PublishMemberChangesAsync(guildId, before, userIds: null, cancellationToken);
        return true;
    }

    private async Task ApplyMemberEventAsync(PlatformRef guild, PlatformMember member, bool revive, CancellationToken cancellationToken)
    {
        using var _ = await syncLock.AcquireAsync(cancellationToken);
        var guildId = await ids.ResolveGuildAsync(guild, cancellationToken);
        var userId = await ResolveMemberAsync(guild, member, cancellationToken);

        var before = await LoadAccessStatesAsync(guildId, [userId], cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await ApplyMemberAsync(guild, guildId, member, revive, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await RecomputeAdministratorsAsync(guildId, [userId], cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await PublishMemberChangesAsync(guildId, before, [userId], cancellationToken);
    }

    private async Task ApplyRoleEventAsync(PlatformRef guild, PlatformRole role, bool revive, CancellationToken cancellationToken)
    {
        using var _ = await syncLock.AcquireAsync(cancellationToken);
        var guildId = await ids.ResolveGuildAsync(guild, cancellationToken);
        var roleId = await ids.ResolveRoleAsync(guild, role.Role, cancellationToken);

        var holders = await HoldersAsync(roleId, cancellationToken);
        var before = await LoadAccessStatesAsync(guildId, holders, cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await ApplyRoleAsync(guild, role, revive, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await RecomputeAdministratorsAsync(guildId, holders, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await PublishMemberChangesAsync(guildId, before, holders, cancellationToken);
    }

    private async Task ApplyChannelEventAsync(PlatformRef guild, PlatformChannel channel, bool revive, CancellationToken cancellationToken)
    {
        using var _ = await syncLock.AcquireAsync(cancellationToken);
        await ApplyChannelAsync(guild, channel, revive, cancellationToken);
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

    /// <remarks>With <c>revive</c> a deleted role comes back (create, snapshot); an update leaves it deleted.</remarks>
    private async Task<Guid> ApplyRoleAsync(PlatformRef guild, PlatformRole snapshot, bool revive, CancellationToken cancellationToken)
    {
        var roleId = await ids.ResolveRoleAsync(guild, snapshot.Role, cancellationToken);
        var role = await db.Roles.SingleAsync(r => r.Id == roleId, cancellationToken);
        role.Name = snapshot.Name;
        role.Color = snapshot.Color;
        role.Position = snapshot.Position;
        role.IsAdministrator = snapshot.IsAdministrator;
        role.ManagesRoles = snapshot.ManagesRoles;
        if (revive)
        {
            role.DeletedAt = null;
        }

        return roleId;
    }

    /// <remarks>With <c>revive</c> a deleted channel comes back (create, snapshot); an update leaves it deleted.</remarks>
    private async Task<Guid> ApplyChannelAsync(PlatformRef guild, PlatformChannel snapshot, bool revive, CancellationToken cancellationToken)
    {
        var channelId = await ids.ResolveChannelAsync(guild, snapshot.Channel, cancellationToken);
        var categoryId = snapshot.Category is { } category ? await ids.ResolveChannelAsync(guild, category, cancellationToken) : (Guid?)null;
        var threadParentId = snapshot.ThreadParent is { } parent ? await ids.ResolveChannelAsync(guild, parent, cancellationToken) : (Guid?)null;

        var channel = await db.Channels.SingleAsync(c => c.Id == channelId, cancellationToken);
        channel.Name = snapshot.Name;
        channel.Type = snapshot.Type;
        channel.CategoryId = categoryId;
        channel.ThreadParentId = threadParentId;
        if (revive)
        {
            channel.DeletedAt = null;
        }

        return channelId;
    }

    /// <summary>
    /// Stores the member's user columns, membership and exact role list. The Administrator flag follows from the saved
    /// roles in <see cref="RecomputeAdministratorsAsync"/>.
    /// </summary>
    /// <remarks>
    /// With <c>revive</c> a member who left comes back (join, snapshot). Without it (an update), a member who left is
    /// left alone: the update predates the leave.
    /// </remarks>
    private async Task<Guid> ApplyMemberAsync(PlatformRef guild, Guid guildId, PlatformMember snapshot, bool revive, CancellationToken cancellationToken)
    {
        var userId = await ids.ResolveUserAsync(snapshot.User, cancellationToken);
        var member = await db.GuildMembers.SingleOrDefaultAsync(m => m.GuildId == guildId && m.UserId == userId, cancellationToken);
        if (member is { LeftAt: not null } && !revive)
        {
            return userId;
        }

        var user = await db.Users.SingleAsync(u => u.Id == userId, cancellationToken);
        user.DisplayName = snapshot.DisplayName;
        user.AvatarUrl = snapshot.AvatarUrl;

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

        // A member update from before a role was deleted may still list that role; a deleted role is held by nobody.
        roleIds.ExceptWith(await db.Roles
            .Where(r => roleIds.Contains(r.Id) && r.DeletedAt != null)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken));

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

    /// <summary>
    /// Marks the channels deleted, and the threads under them: a thread cannot outlive its parent channel.
    /// </summary>
    private async Task MarkChannelsDeletedAsync(IReadOnlyCollection<Channel> channels, DateTimeOffset deletedAt, CancellationToken cancellationToken)
    {
        if (channels.Count == 0)
        {
            return;
        }

        var channelIds = channels.Select(c => c.Id).ToList();
        var threads = await db.Channels
            .Where(c => c.ThreadParentId != null && channelIds.Contains(c.ThreadParentId.Value) && c.DeletedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var channel in channels.Concat(threads))
        {
            channel.DeletedAt ??= deletedAt;
        }
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
                .Join(db.Roles.Where(role => role.DeletedAt == null), memberRole => memberRole.RoleId, role => role.Id, (memberRole, role) => new { memberRole.UserId, role.IsAdministrator })
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
        Message = "Synchronized guild {Guild}: {RoleCount} roles, {ChannelCount} channels; marked {DeletedRoleCount} roles and {DeletedChannelCount} channels deleted")]
    private partial void LogStructureSynchronized(PlatformRef guild, int roleCount, int channelCount, int deletedRoleCount, int deletedChannelCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Synchronized members of guild {Guild}: {MemberCount} members, marked {LeftCount} left")]
    private partial void LogMembersSynchronized(PlatformRef guild, int memberCount, int leftCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Member list of guild {Guild} failed (attempt {Attempt} of {Attempts}); no member is marked left")]
    private partial void LogMemberListFailed(Exception exception, PlatformRef guild, int attempt, int attempts);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Member list of guild {Guild} came back empty (attempt {Attempt} of {Attempts}); no member is marked left")]
    private partial void LogMemberListEmpty(PlatformRef guild, int attempt, int attempts);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopped the member pass of guild {Guild}: a newer snapshot took over or the bot is stopping")]
    private partial void LogMemberPassStopped(PlatformRef guild);

    [LoggerMessage(Level = LogLevel.Error, Message = "Members of guild {Guild} not synchronized after {Attempts} attempts; they stay as they were until the next snapshot")]
    private partial void LogMembersNotSynchronized(PlatformRef guild, int attempts);

    private readonly record struct AccessState(bool IsMember, bool IsAdministrator, string Roles);
}
