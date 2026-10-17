using Apollo.Bot.Tracking;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;

namespace Apollo.Bot.Privacy;

/// <inheritdoc cref="IUserDataEraser"/>
/// <remarks>
/// <para>The order matters:</para>
/// <list type="number">
/// <item>
/// The gate marks the member untracked first (<see cref="TrackingGate.MarkUntrackedAsync"/>), so events on their way are
/// dropped and their open voice and presence sessions close through the usual tracking hooks before anything is
/// deleted. Rows the close writes are deleted in step 2, so nothing written before the deletion survives it.
/// </item>
/// <item>
/// One transaction runs every <see cref="IUserDataEraserStep"/>, so a failure deletes nothing.
/// </item>
/// <item>
/// Only then is the tracking role removed: after a failed deletion the member keeps their role and their data as before,
/// and after a successful one recording does not resume. The gate's mark ends once the role removal is synced (the
/// echoed member update). Without a role to remove (bootstrap mode, tracking not set up, the member does not hold it) or
/// when the platform refuses the removal, the mark ends right away and the gate tracks whoever the rule tracks, from a
/// new tracking period.
/// </item>
/// </list>
/// <para>
/// An event a recorder checked against the gate just before step 1 may still be stored after step 2 (see
/// <see cref="TrackingGate"/>); the window is the time of one database write.
/// </para>
/// </remarks>
internal sealed partial class UserDataEraser(
    ApolloDbContext db,
    IPlatformIdResolver ids,
    TrackingGate gate,
    IChatPlatform platform,
    IEnumerable<IUserDataEraserStep> steps,
    ILogger<UserDataEraser> logger) : IUserDataEraser
{
    public async Task<UserDataErasureOutcome> EraseAsync(PlatformRef guild, PlatformRef member, CancellationToken cancellationToken)
    {
        // One view for the whole deletion, so the mode, the tracking role and whether the member holds it belong together.
        // With a tracking role, the gate tracks exactly its holders (see TrackingRoleSwitch).
        var view = gate.Current;
        var role = view.TrackingRole is { } trackingRole && view.TryGetTrackedUserId(member) is not null ? trackingRole : null;

        // A member the bot has never seen has nothing stored, and nothing is created for them.
        if (await ids.TryGetGuildAsync(guild, cancellationToken) is not { } guildId
            || await ids.TryGetUserAsync(member, cancellationToken) is not { } userId)
        {
            return WithoutRoleRemoval(view);
        }

        try
        {
            await gate.MarkUntrackedAsync(guildId, userId, cancellationToken);
            await EraseRowsAsync(guildId, userId, cancellationToken);
        }
        catch (Exception exception) when (!IsCancellation(exception, cancellationToken))
        {
            LogEraseFailed(exception, userId);
            await gate.ReleaseUntrackedMarkAsync(guildId, userId, CancellationToken.None);
            return UserDataErasureOutcome.Failed;
        }

        if (role is null)
        {
            await gate.ReleaseUntrackedMarkAsync(guildId, userId, cancellationToken);
            return WithoutRoleRemoval(view);
        }

        try
        {
            await platform.RemoveRoleAsync(guild, member, role, cancellationToken);
        }
        catch (Exception exception) when (!IsCancellation(exception, cancellationToken))
        {
            LogRemoveRoleFailed(exception, role.ToString(), userId);
            await gate.ReleaseUntrackedMarkAsync(guildId, userId, CancellationToken.None);
            return UserDataErasureOutcome.DeletedButRoleKept;
        }

        return UserDataErasureOutcome.DeletedAndRoleRemoved;
    }

    private static bool IsCancellation(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    private static UserDataErasureOutcome WithoutRoleRemoval(ITrackingView view) =>
        view.TrackingRole is not null ? UserDataErasureOutcome.Deleted
        : view.Mode == TrackingMode.Bootstrap ? UserDataErasureOutcome.DeletedInBootstrapMode // TEMPORARY: removed by #62
        : UserDataErasureOutcome.DeletedNotSetUp;

    private async Task EraseRowsAsync(Guid guildId, Guid userId, CancellationToken cancellationToken)
    {
        var erased = new List<ErasedRows>();
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            foreach (var step in steps)
            {
                erased.AddRange(await step.EraseAsync(guildId, userId, cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
        }

        LogErased(
            userId,
            erased.Sum(e => e.Deleted),
            erased.Sum(e => e.Cleared),
            string.Join("; ", erased.Select(e => e.Cleared > 0 ? $"{e.Table}: {e.Deleted} deleted, {e.Cleared} cleared" : $"{e.Table}: {e.Deleted} deleted")));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Erased the data of {UserId}: {DeletedRows} rows deleted, {ClearedReferences} references cleared ({Tables})")]
    private partial void LogErased(Guid userId, int deletedRows, int clearedReferences, string tables);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erasing the data of {UserId} failed; nothing was deleted")]
    private partial void LogEraseFailed(Exception exception, Guid userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erased the data of {UserId}, but could not remove the tracking role {Role}; they stay tracked")]
    private partial void LogRemoveRoleFailed(Exception exception, string role, Guid userId);
}
