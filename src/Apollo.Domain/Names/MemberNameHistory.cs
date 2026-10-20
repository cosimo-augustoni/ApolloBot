namespace Apollo.Domain.Names;

/// <summary>
/// Decides whether observed names extend a tracked member's name history (#114, scope §5.1).
/// </summary>
/// <remarks>
/// <para>
/// Every tracking period starts with a <see cref="MemberNameChangeKind.Baseline"/>: the first row stamped at or after
/// the period's start. After it, a <see cref="MemberNameChangeKind.Changed"/> row is stored only when one of the three
/// names differs from the latest row, so a replayed update and an update that changed only the roles store nothing.
/// </para>
/// <para>
/// Names observed before the latest row was stamped are stale: that row was stamped either on receipt of a later update
/// or by a sync or tracking start that read the platform's current state, which already includes the observed update
/// (it waited for the recorder's lock). They store nothing, so an update that waited can never undo newer names or
/// produce rows out of order. A baseline observed before the period started (an update received just before the gate
/// tracked the member) is stamped at the start, so no row lies outside a tracking period.
/// </para>
/// </remarks>
public static class MemberNameHistory
{
    /// <summary>
    /// The row to store for <paramref name="observed"/>, or <see langword="null"/> when the history already holds it.
    /// </summary>
    /// <param name="observed">The member's names as the platform reports them.</param>
    /// <param name="observedAt">When they were observed: on receipt of an update, or now for a sync or tracking start.</param>
    /// <param name="trackedSince">When the member's open tracking period started.</param>
    /// <param name="latest">The member's latest stored row, from any period; <see langword="null"/> without one.</param>
    public static MemberNameEntry? Next(MemberNames observed, DateTimeOffset observedAt, DateTimeOffset trackedSince, RecordedMemberNames? latest)
    {
        if (latest is null || latest.At < trackedSince)
        {
            return new MemberNameEntry(MemberNameChangeKind.Baseline, observed, observedAt < trackedSince ? trackedSince : observedAt);
        }

        return observedAt < latest.At || observed == latest.Names
            ? null
            : new MemberNameEntry(MemberNameChangeKind.Changed, observed, observedAt);
    }
}
