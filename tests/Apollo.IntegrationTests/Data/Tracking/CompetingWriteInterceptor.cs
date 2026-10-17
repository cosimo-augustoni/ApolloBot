using Apollo.Data.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Apollo.IntegrationTests.Data.Tracking;

/// <summary>
/// Runs <c>competingWrite</c> (another admin's save, on its own connection) once, right before the first save that
/// writes <see cref="RoleSetting"/> rows in <c>state</c>: a deterministic concurrent save.
/// </summary>
internal sealed class CompetingWriteInterceptor(EntityState state, Func<Task> competingWrite) : SaveChangesInterceptor
{
    private bool _done;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (!_done && eventData.Context!.ChangeTracker.Entries<RoleSetting>().Any(e => e.State == state))
        {
            _done = true;
            await competingWrite();
        }

        return result;
    }
}
