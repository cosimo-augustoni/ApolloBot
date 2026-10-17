using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Apollo.Bot.Tests.Privacy;

/// <summary>
/// Fails every <c>DELETE</c> from <paramref name="table"/>, as a database error in the middle of a data deletion would.
/// </summary>
internal sealed class FailingDeleteInterceptor(string table) : DbCommandInterceptor
{
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var text = command.CommandText.TrimStart();
        if (text.StartsWith($"DELETE FROM {table} ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith($"DELETE FROM \"{table}\"", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Simulated: database write failed.");
        }

        return ValueTask.FromResult(result);
    }
}
