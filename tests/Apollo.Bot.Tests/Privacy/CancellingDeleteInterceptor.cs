using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Apollo.Bot.Tests.Privacy;

/// <summary>
/// Cancels <see cref="Source"/> at the first <c>DELETE</c> and fails it with its cancellation, as a deletion stopped in
/// the middle (e.g. the bot shutting down) would.
/// </summary>
internal sealed class CancellingDeleteInterceptor : DbCommandInterceptor, IDisposable
{
    public CancellationTokenSource Source { get; } = new();

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.TrimStart().StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            await Source.CancelAsync();
            throw new OperationCanceledException(Source.Token);
        }

        return result;
    }

    public void Dispose() => Source.Dispose();
}
