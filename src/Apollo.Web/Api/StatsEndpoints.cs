using System.Globalization;
using Apollo.Web.Admin;
using Apollo.Web.Client.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apollo.Web.Api;

/// <summary>
/// The stats API of the admin area under <c>/api/admin/stats</c>, in the <see cref="AdminEndpoints.MapAdminGroup"/> group:
/// <c>server?from=yyyy-MM-dd&amp;to=yyyy-MM-dd</c> (#37) is the activity of the configured guild over those Zurich days,
/// both included, bots left out. A missing or malformed date, or <c>from</c> after <c>to</c>, is 400 with a validation
/// problem.
/// </summary>
public static class StatsEndpoints
{
    public const string GroupPath = "/stats";

    /// <summary>The only date format the range accepts (ISO 8601 calendar date).</summary>
    public const string DateFormat = "yyyy-MM-dd";

    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var stats = endpoints.MapAdminGroup().MapGroup(GroupPath);

        stats.MapGet("/server", GetServerAsync);

        return endpoints;
    }

    // The dates bind as strings, so a malformed one is a validation problem like every other bad range, not a bind failure.
    private static async Task<Results<Ok<ActivityStatsDto>, ValidationProblem>> GetServerAsync(
        string? from, string? to, ServerStatsQuery query, CancellationToken cancellationToken)
    {
        if (ParseDate(from) is not { } start)
        {
            return Invalid(nameof(from), $"The start must be a date as {DateFormat}.");
        }

        if (ParseDate(to) is not { } end)
        {
            return Invalid(nameof(to), $"The end must be a date as {DateFormat}.");
        }

        if (start > end)
        {
            return Invalid(nameof(from), "The range must not end before it starts.");
        }

        return TypedResults.Ok(await query.GetAsync(start, end, cancellationToken));
    }

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
