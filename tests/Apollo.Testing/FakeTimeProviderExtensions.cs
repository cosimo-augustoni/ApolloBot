
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Apollo.Testing;

public static class FakeTimeProviderExtensions
{
    /// <summary>
    /// Advances fake time by <paramref name="step"/> until <paramref name="task"/> completes, so retry and poll delays
    /// elapse without waiting on the wall clock. The short real pause lets the task reach its next delay.
    /// </summary>
    public static async Task<T> AdvanceUntilCompletedAsync<T>(this FakeTimeProvider time, Task<T> task, TimeSpan step)
    {
        await AdvanceUntilCompletedAsync(time, (Task)task, step);
        return await task;
    }

    public static async Task AdvanceUntilCompletedAsync(this FakeTimeProvider time, Task task, TimeSpan step)
    {
        while (!task.IsCompleted)
        {
            await Task.WhenAny(task, Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken));
            time.Advance(step);
        }

        await task;
    }
}
