using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Apollo.Data.Activity;

/// <inheritdoc cref="IAggregateRefresher"/>
/// <remarks>
/// <para>
/// The range is widened to whole Zurich days, the buckets of the daily aggregates (their bucket is the UTC instant of a
/// Zurich midnight, #34); those bounds are hour bounds too, so the hourly heatmap is covered whole as well. DST days
/// have 23 or 25 hours.
/// </para>
/// <para>
/// Every continuous aggregate in the database is refreshed, read from the TimescaleDB catalog, so an aggregate a later
/// migration adds is covered without a second list to keep. <c>refresh_continuous_aggregate</c> re-materializes only the
/// buckets that changed since the last refresh, so the rest of the range costs little.
/// </para>
/// <para>
/// <c>refresh_continuous_aggregate</c> cannot run inside a transaction block, so this throws while the context has one
/// open: call it after the commit.
/// </para>
/// </remarks>
public sealed partial class ContinuousAggregateRefresher(ApolloDbContext db, ILogger<ContinuousAggregateRefresher> logger) : IAggregateRefresher
{
    private static readonly TimeZoneInfo _zurich = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    public async Task RefreshAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Continuous aggregates cannot be refreshed inside a transaction; refresh after the commit.");
        }

        var start = StartOf(ZurichDayOf(from));
        var end = StartOf(ZurichDayOf(to).AddDays(1));

        var aggregates = await db.Database
            .SqlQuery<string>($"""SELECT format('%I.%I', view_schema, view_name) AS "Value" FROM timescaledb_information.continuous_aggregates ORDER BY 1""")
            .ToListAsync(cancellationToken);

        foreach (var aggregate in aggregates)
        {
            await db.Database.ExecuteSqlAsync(
                $"CALL refresh_continuous_aggregate({aggregate}::regclass, {start}::timestamptz, {end}::timestamptz)",
                cancellationToken);
        }

        LogRefreshed(string.Join(", ", aggregates), start, end);
    }

    private static DateOnly ZurichDayOf(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, _zurich).DateTime);

    // Midnight always exists in Zurich: the DST switches are at 02:00 and 03:00.
    private static DateTimeOffset StartOf(DateOnly day) =>
        new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), _zurich), TimeSpan.Zero);

    [LoggerMessage(Level = LogLevel.Information, Message = "Refreshed the continuous aggregates {Aggregates} from {From} to {To}")]
    private partial void LogRefreshed(string aggregates, DateTimeOffset from, DateTimeOffset to);
}
