namespace Apollo.Bot.Privacy;

/// <summary>
/// One part of a data deletion (#22): deletes a user's rows from some tables and clears references to them in other
/// users' rows. Register with <see cref="PrivacyCommandsServiceCollectionExtensions.AddUserDataEraserStep{TStep}"/>;
/// a feature that stores data about users adds a step (e.g. recap snapshots in M5).
/// </summary>
/// <remarks>
/// Steps are scoped and run one after another in the deletion's transaction on the scoped <c>ApolloDbContext</c>, so a
/// failing step rolls back all of them. Work that cannot run in a transaction (refreshing continuous aggregates, #39)
/// belongs after the commit, not in a step: <see cref="UserDataEraser"/> asks every step for its
/// <see cref="FindRangeAsync"/> before any step erases, and refreshes the aggregates over that range after the commit.
/// </remarks>
public interface IUserDataEraserStep
{
    /// <summary>
    /// The time range of the rows <see cref="EraseAsync"/> would delete or clear a reference in, read in the deletion's
    /// transaction before any step erases. <see langword="null"/> when there are none, or when the step's tables feed no
    /// continuous aggregate.
    /// </summary>
    Task<ErasedRange?> FindRangeAsync(Guid guildId, Guid userId, CancellationToken cancellationToken);

    /// <returns>The rows deleted and references cleared, per table, for the deletion log.</returns>
    Task<IReadOnlyList<ErasedRows>> EraseAsync(Guid guildId, Guid userId, CancellationToken cancellationToken);
}
