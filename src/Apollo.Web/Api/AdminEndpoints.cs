using Apollo.Web.Client.Auth;

namespace Apollo.Web.Api;

public static class AdminEndpoints
{
    public const string GroupPath = "/api/admin";

    /// <summary>
    /// The <c>/api/admin</c> route group with the <see cref="ApolloPolicies.Admin"/> policy (#30): anonymous callers get
    /// 401, logged-in users without it 403. Every admin endpoint is mapped into it (bot status #31, data #32, settings #33).
    /// </summary>
    public static RouteGroupBuilder MapAdminGroup(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(GroupPath);
        group.RequireAuthorization(ApolloPolicies.Admin);
        return group;
    }
}
