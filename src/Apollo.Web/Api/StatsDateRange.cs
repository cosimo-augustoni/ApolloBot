using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Apollo.Web.Admin;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apollo.Web.Api;

/// <summary>
/// The <c>from</c>/<c>to</c> query of the stats endpoints (#37, #38): Zurich days as <see cref="DateFormat"/>, both
/// included. A missing or malformed date, <c>from</c> after <c>to</c>, or a date outside <see cref="EarliestDate"/> to
/// tomorrow in Zurich is a validation problem.
/// </summary>
public static class StatsDateRange
{
    /// <summary>The only date format the range accepts (ISO 8601 calendar date).</summary>
    public const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// The earliest day a range may start on: long before any recorded data. Far older days fall into Zurich's old
    /// local mean time, whose midnight lies before <see cref="DateTime.MinValue"/> in UTC.
    /// </summary>
    public static readonly DateOnly EarliestDate = new(2000, 1, 1);

    /// <summary>How many days after today (Zurich) a range may end, for a browser whose clock or zone is a little ahead.</summary>
    public const int FutureSlackDays = 1;

    /// <summary>
    /// Parses the range; on failure <paramref name="problem"/> names the field. The dates bind as strings, so a
    /// malformed one is a validation problem like every other bad range, not a bind failure.
    /// </summary>
    public static bool TryParse(
        string? from,
        string? to,
        TimeProvider time,
        out (DateOnly From, DateOnly To) range,
        [NotNullWhen(false)] out ValidationProblem? problem)
    {
        ArgumentNullException.ThrowIfNull(time);
        range = default;
        problem = null;
        if (ParseDate(from) is not { } start)
        {
            problem = Invalid(nameof(from), $"The start must be a date as {DateFormat}.");
        }
        else if (ParseDate(to) is not { } end)
        {
            problem = Invalid(nameof(to), $"The end must be a date as {DateFormat}.");
        }
        else if (start > end)
        {
            problem = Invalid(nameof(from), "The range must not end before it starts.");
        }

        // Bounded, so no day overflows the instant arithmetic and no range zero-fills centuries of days.
        else if (start < EarliestDate)
        {
            problem = Invalid(nameof(from), $"The start must not be before {EarliestDate.ToString(DateFormat, CultureInfo.InvariantCulture)}.");
        }
        else if (end > Latest(time))
        {
            problem = Invalid(nameof(to), $"The end must not be after {Latest(time).ToString(DateFormat, CultureInfo.InvariantCulture)}.");
        }
        else
        {
            range = (start, end);
        }

        return problem is null;
    }

    private static DateOnly Latest(TimeProvider time) => ZurichTime.DayOf(time.GetUtcNow()).AddDays(FutureSlackDays);

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
