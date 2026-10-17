using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Apollo.Bot.Tests.Presence;

/// <summary>
/// Once <see cref="Arm"/>ed, fails the next insert into <c>presence_activity_sessions</c> (storing a closed session), as
/// a transient database error would.
/// </summary>
internal sealed class FailingPresenceInsertInterceptor : DbCommandInterceptor
{
    private int _armed;

    public void Arm() => Interlocked.Exchange(ref _armed, 1);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("INSERT INTO presence_activity_sessions", StringComparison.OrdinalIgnoreCase)
            && Interlocked.Exchange(ref _armed, 0) == 1)
        {
            throw new InvalidOperationException("Simulated: database write failed.");
        }

        return ValueTask.FromResult(result);
    }
}
