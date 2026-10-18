using Apollo.Data;
using Apollo.Data.Activity;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Bot.Tests.Privacy;

/// <summary>
/// An <see cref="IAggregateRefresher"/> that records its calls into a singleton <see cref="AggregateRefreshLog"/>, with
/// the state of the scoped <see cref="ApolloDbContext"/> the deletion uses, and refreshes nothing.
/// </summary>
internal sealed class RecordingAggregateRefresher(ApolloDbContext db, AggregateRefreshLog log) : IAggregateRefresher
{
    public Task RefreshAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        log.Calls.Add(new AggregateRefreshLog.Call(from, to, db.Database.CurrentTransaction is not null));
        return log.Failure is { } failure ? Task.FromException(failure) : Task.CompletedTask;
    }

    /// <summary>
    /// Replaces the registered refresher with one that records into <paramref name="log"/>.
    /// </summary>
    public static Action<IServiceCollection> Replace(AggregateRefreshLog log) => services => services
        .AddSingleton(log)
        .AddScoped<IAggregateRefresher, RecordingAggregateRefresher>();
}
