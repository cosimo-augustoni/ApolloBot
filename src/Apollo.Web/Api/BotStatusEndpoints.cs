using Apollo.Web.Admin;
using Apollo.Web.Client.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apollo.Web.Api;

/// <summary>
/// <c>GET /api/admin/bot-status</c> (#31) in the <see cref="AdminEndpoints.MapAdminGroup"/> group: the bot's latest
/// heartbeat and the events recorded per hour, from <see cref="BotStatusQuery"/>.
/// </summary>
public static class BotStatusEndpoints
{
    public const string Path = "/bot-status";

    public static IEndpointRouteBuilder MapBotStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAdminGroup().MapGet(Path, GetAsync);
        return endpoints;
    }

    private static async Task<Ok<BotStatusDto>> GetAsync(BotStatusQuery query, CancellationToken cancellationToken) =>
        TypedResults.Ok(await query.GetAsync(cancellationToken));
}
