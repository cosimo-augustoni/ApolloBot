namespace Apollo.Bot.Privacy;

/// <summary>
/// One part of a data deletion (#22): deletes a user's rows from some tables and clears references to them in other
/// users' rows. Register with <see cref="PrivacyCommandsServiceCollectionExtensions.AddUserDataEraserStep{TStep}"/>;
/// a feature that stores data about users adds a step (e.g. recap snapshots in M5).
/// </summary>
/// <remarks>
/// Steps are scoped and run one after another in the deletion's transaction on the scoped <c>ApolloDbContext</c>, so a
/// failing step rolls back all of them. Work that cannot run in a transaction (refreshing continuous aggregates, #39)
/// belongs after the commit, not in a step.
/// </remarks>
public interface IUserDataEraserStep
{
    /// <returns>The rows deleted and references cleared, per table, for the deletion log.</returns>
    Task<IReadOnlyList<ErasedRows>> EraseAsync(Guid guildId, Guid userId, CancellationToken cancellationToken);
}
