using System.Data.Common;
using Npgsql;

namespace Apollo.Data;

/// <summary>
/// Classifies exceptions by walking the inner-exception chain, since EF wraps provider errors
/// (e.g. <c>RetryLimitExceededException</c> or the "transient failure" <see cref="InvalidOperationException"/>).
/// </summary>
internal static class DatabaseErrors
{
    /// <summary>
    /// The database could not be reached right now (connection refused, timeout, server starting up).
    /// </summary>
    public static bool IsTransient(Exception exception) =>
        Chain(exception).Any(e => e is NpgsqlException { IsTransient: true });

    /// <summary>
    /// Any error reported by or on the way to the database, including "database does not exist".
    /// </summary>
    public static bool IsDatabaseError(Exception exception) =>
        Chain(exception).Any(e => e is DbException);

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }
}
