using Apollo.Data.Tracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Apollo.Bot.Tests.Tracking;

/// <summary>
/// Fails the first <c>failures</c> saves that write <c>tracking_periods</c>, as a transient database error would.
/// Saves of the guild state sync pass.
/// </summary>
internal sealed class FailingPeriodSaveInterceptor(int failures) : SaveChangesInterceptor
{
    private int _saves;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<TrackingPeriod>().Any() && Interlocked.Increment(ref _saves) <= failures)
        {
            throw new InvalidOperationException("Simulated: database write failed.");
        }

        return ValueTask.FromResult(result);
    }
}
