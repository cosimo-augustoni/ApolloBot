namespace Apollo.Bot.Tracking;

/// <summary>
/// How much data is stored about a user: the rows per event table, for <c>/apollo tracking status</c> (scope §5.4).
/// </summary>
public interface IUserDataSummary
{
    /// <summary>
    /// Counts the user's rows in every event table of the guild, from all tracking periods.
    /// </summary>
    Task<UserDataCounts> CountAsync(Guid guildId, Guid userId, CancellationToken cancellationToken);
}
