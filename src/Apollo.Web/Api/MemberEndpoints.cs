using Apollo.Web.Admin;
using Apollo.Web.Client.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apollo.Web.Api;

/// <summary>
/// The member API of the admin area under <c>/api/admin/members</c>, in the <see cref="AdminEndpoints.MapAdminGroup"/>
/// group: <c>{userId}/stats?from=yyyy-MM-dd&amp;to=yyyy-MM-dd</c> (#38) is one member's stats page over those Zurich
/// days, both included, bots left out. A bad range is 400 with a validation problem (<see cref="StatsDateRange"/>); a
/// user who is no member of the configured guild (departed ones included) is 404.
/// </summary>
public static class MemberEndpoints
{
    public const string GroupPath = "/members";

    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var members = endpoints.MapAdminGroup().MapGroup(GroupPath);

        members.MapGet("/{userId:guid}/stats", GetStatsAsync);

        return endpoints;
    }

    private static async Task<Results<Ok<MemberStatsDto>, NotFound, ValidationProblem>> GetStatsAsync(
        Guid userId, string? from, string? to, MemberStatsQuery query, TimeProvider time, CancellationToken cancellationToken)
    {
        if (!StatsDateRange.TryParse(from, to, time, out var range, out var problem))
        {
            return problem;
        }

        return await query.GetAsync(userId, range.From, range.To, cancellationToken) is { } stats
            ? TypedResults.Ok(stats)
            : TypedResults.NotFound();
    }
}
