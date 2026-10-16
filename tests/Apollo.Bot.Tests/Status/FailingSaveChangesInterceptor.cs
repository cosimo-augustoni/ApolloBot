using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Apollo.Bot.Tests.Status;

/// <summary>
/// Fails the first <c>failures</c> saves, as a database error while writing would.
/// </summary>
internal sealed class FailingSaveChangesInterceptor(int failures) : SaveChangesInterceptor
{
    private int _saves;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _saves) <= failures)
        {
            throw new InvalidOperationException("Simulated: database write failed.");
        }

        return ValueTask.FromResult(result);
    }
}
