using Apollo.Stats.Sanity;
using Apollo.Web.Admin;
using Apollo.Web.Client.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apollo.Web.Api;

/// <summary>
/// The data sanity API of the admin area (#40) under <c>/api/admin/sanity</c>, in the
/// <see cref="AdminEndpoints.MapAdminGroup"/> group: <c>?from=yyyy-MM-dd&amp;to=yyyy-MM-dd</c> is the report of the
/// configured guild over those Zurich days, both included. Without both dates it covers the last
/// <see cref="DataSanityQuery.DefaultDays"/> days up to today; any other bad range is 400 with a validation problem
/// (<see cref="StatsDateRange"/>).
/// </summary>
public static class SanityEndpoints
{
    public const string GroupPath = "/sanity";

    public static IEndpointRouteBuilder MapSanityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAdminGroup().MapGet(GroupPath, GetAsync);

        return endpoints;
    }

    private static async Task<Results<Ok<DataSanityDto>, ValidationProblem>> GetAsync(
        string? from, string? to, SanityReportQuery query, TimeProvider time, CancellationToken cancellationToken)
    {
        var range = DataSanityQuery.DefaultRange(time);
        if ((from is not null || to is not null) && !StatsDateRange.TryParse(from, to, time, out range, out var problem))
        {
            return problem;
        }

        return TypedResults.Ok(await query.GetAsync(range.From, range.To, cancellationToken));
    }
}
