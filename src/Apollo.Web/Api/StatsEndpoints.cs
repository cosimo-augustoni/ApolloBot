using Apollo.Web.Admin;
using Apollo.Web.Client.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apollo.Web.Api;

/// <summary>
/// The stats API of the admin area under <c>/api/admin/stats</c>, in the <see cref="AdminEndpoints.MapAdminGroup"/> group:
/// <c>server?from=yyyy-MM-dd&amp;to=yyyy-MM-dd</c> (#37) is the activity of the configured guild over those Zurich days,
/// both included, bots left out. A bad range is 400 with a validation problem (<see cref="StatsDateRange"/>).
/// </summary>
public static class StatsEndpoints
{
    public const string GroupPath = "/stats";

    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var stats = endpoints.MapAdminGroup().MapGroup(GroupPath);

        stats.MapGet("/server", GetServerAsync);

        return endpoints;
    }

    private static async Task<Results<Ok<ActivityStatsDto>, ValidationProblem>> GetServerAsync(
        string? from, string? to, ServerStatsQuery query, TimeProvider time, CancellationToken cancellationToken)
    {
        if (!StatsDateRange.TryParse(from, to, time, out var range, out var problem))
        {
            return problem;
        }

        return TypedResults.Ok(await query.GetAsync(range.From, range.To, cancellationToken));
    }
}
