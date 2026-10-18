using Apollo.Bot.Tracking;
using Apollo.Data;
using Apollo.Data.Activity;
using Apollo.Data.Identity;
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
/// One transaction runs every <see cref="IUserDataEraserStep"/>, so a failure deletes nothing. It first reads the
/// time range of the rows the steps change (<see cref="IUserDataEraserStep.FindRangeAsync"/>).
/// </item>
/// <item>
/// Only then is the tracking role removed: after a failed deletion the member keeps their role and their data as before,
/// and after a successful one recording does not resume. The gate's mark ends once the role removal is synced (the
/// echoed member update). Without a role to remove (tracking not set up, the member does not hold it) or
/// when the platform refuses the removal, the deletion fails or is cancelled, the mark ends right away and the gate
/// tracks whoever the rule tracks, from a new tracking period. Only the deletion that added the mark ends it.
/// </item>
/// <item>
/// Last, the continuous aggregates are refreshed over that range (#39): a refresh policy only re-materializes its own
/// window, so older aggregate rows would keep the deleted numbers. A refresh cannot run in a transaction, and after the
/// role removal a cancellation cannot leave a member with deleted data still holding the role. When it fails the deletion
/// stays: the error is logged, and the aggregates are fixed by a manual refresh (docs/scope.md §5.4). Without rows there
/// is nothing to refresh.
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
    IAggregateRefresher aggregates,
    ILogger<UserDataEraser> logger) : IUserDataEraser
{
    public async Task<UserDataErasureOutcome> EraseAsync(PlatformRef guild, PlatformRef member, CancellationToken cancellationToken)
    {
        // One view for the whole deletion, so the mode, the tracking role and whether the member holds it belong together.
        var view = gate.Current;

        // A member the bot has never seen has nothing stored, and nothing is created for them.
        if (await ids.TryGetGuildAsync(guild, cancellationToken) is not { } guildId
            || await ids.TryGetUserAsync(member, cancellationToken) is not { } userId)
        {
            return WithoutRoleRemoval(view);
        }

        // Only a mark this call added is released here, on every way out, unless the role removal ends it. A second
        // confirm while the first one's role removal is not synced yet finds the mark and leaves it to the first.
        var mark = UntrackedMark.NotTracked;
        var markEndsWithRoleRemoval = false;
        try
        {
            ErasedRange? range;
            try
            {
                mark = await gate.MarkUntrackedAsync(guildId, userId, cancellationToken);
                range = await EraseRowsAsync(guildId, userId, cancellationToken);
            }
            catch (Exception exception) when (!IsCancellation(exception, cancellationToken))
            {
                LogEraseFailed(exception, userId);
                return UserDataErasureOutcome.Failed;
            }

            // The refresh comes last, after the role removal on every path: a cancellation during a long refresh (e.g. a
            // shutdown) must not skip the removal once the rows are gone, or the member would be recorded again.
            var outcome = await RemoveTrackingRoleAsync(view, guild, member, mark, userId, cancellationToken);
            markEndsWithRoleRemoval = outcome == UserDataErasureOutcome.DeletedAndRoleRemoved;
            if (range is not null)
            {
                await RefreshAggregatesAsync(userId, range, cancellationToken);
            }

            return outcome;
        }
        finally
        {
            if (mark == UntrackedMark.Added && !markEndsWithRoleRemoval)
            {
                await gate.ReleaseUntrackedMarkAsync(guildId, userId, CancellationToken.None);
            }
        }
    }

    private async Task<UserDataErasureOutcome> RemoveTrackingRoleAsync(
        ITrackingView view,
        PlatformRef guild,
        PlatformRef member,
        UntrackedMark mark,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // With a tracking role, the gate tracks exactly its holders (see TrackingRoleSwitch). A mark another deletion
        // added means the member held the role then, and its removal may not be synced yet: removing it again is
        // harmless, and the reply stays true.
        var holdsRole = view.TryGetTrackedUserId(member) is not null || mark == UntrackedMark.AlreadyMarked;
        if (view.TrackingRole is not { } role || !holdsRole)
        {
            return WithoutRoleRemoval(view);
        }

        try
        {
            await platform.RemoveRoleAsync(guild, member, role, cancellationToken);
        }
        catch (Exception exception) when (!IsCancellation(exception, cancellationToken))
        {
            LogRemoveRoleFailed(exception, role.ToString(), userId);
            return UserDataErasureOutcome.DeletedButRoleKept;
        }

        return UserDataErasureOutcome.DeletedAndRoleRemoved;
    }

    private static bool IsCancellation(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    private static UserDataErasureOutcome WithoutRoleRemoval(ITrackingView view) =>
        view.TrackingRole is not null ? UserDataErasureOutcome.Deleted : UserDataErasureOutcome.DeletedNotSetUp;

    private async Task<ErasedRange?> EraseRowsAsync(Guid guildId, Guid userId, CancellationToken cancellationToken)
    {
        var erased = new List<ErasedRows>();
        ErasedRange? range = null;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            // Before any step erases: a step may clear what another one's range is read from.
            foreach (var step in steps)
            {
                if (await step.FindRangeAsync(guildId, userId, cancellationToken) is { } stepRange)
                {
                    range = range?.Union(stepRange) ?? stepRange;
                }
            }

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
        return range;
    }

    private async Task RefreshAggregatesAsync(Guid userId, ErasedRange range, CancellationToken cancellationToken)
    {
        try
        {
            await aggregates.RefreshAsync(range.From, range.To, cancellationToken);
            LogRefreshed(userId, range.From, range.To);
        }
        catch (Exception exception) when (!IsCancellation(exception, cancellationToken))
        {
            LogRefreshFailed(exception, userId, range.From, range.To);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Erased the data of {UserId}: {DeletedRows} rows deleted, {ClearedReferences} references cleared ({Tables})")]
    private partial void LogErased(Guid userId, int deletedRows, int clearedReferences, string tables);

    [LoggerMessage(Level = LogLevel.Information, Message = "Refreshed the aggregates after erasing the data of {UserId}, over {From} to {To}")]
    private partial void LogRefreshed(Guid userId, DateTimeOffset from, DateTimeOffset to);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Erased the data of {UserId}, but could not refresh the aggregates over {From} to {To}; they keep the erased numbers until refreshed by hand (docs/deployment.md, Refresh the aggregates by hand)")]
    private partial void LogRefreshFailed(Exception exception, Guid userId, DateTimeOffset from, DateTimeOffset to);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erasing the data of {UserId} failed; nothing was deleted")]
    private partial void LogEraseFailed(Exception exception, Guid userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erased the data of {UserId}, but could not remove the tracking role {Role}; they stay tracked")]
    private partial void LogRemoveRoleFailed(Exception exception, string role, Guid userId);
}
