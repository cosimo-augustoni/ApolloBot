using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Apollo.Bot.Tests.Voice;

/// <summary>
/// Once <see cref="Arm"/>ed, fails the next update of <c>voice_sessions</c> (closing a session), as a transient database
/// error would. Everything before arming passes, e.g. the startup close.
/// </summary>
internal sealed class FailingVoiceCloseInterceptor : DbCommandInterceptor
{
    private int _armed;

    public void Arm() => Interlocked.Exchange(ref _armed, 1);

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.TrimStart().StartsWith("UPDATE voice_sessions", StringComparison.OrdinalIgnoreCase)
            && Interlocked.Exchange(ref _armed, 0) == 1)
        {
            throw new InvalidOperationException("Simulated: database write failed.");
        }

        return ValueTask.FromResult(result);
    }
}
