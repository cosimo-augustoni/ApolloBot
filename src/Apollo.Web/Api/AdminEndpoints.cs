using Apollo.Web.Auth;
using Apollo.Web.Client.Auth;

namespace Apollo.Web.Api;

public static class AdminEndpoints
{
    public const string GroupPath = "/api/admin";

    /// <summary>
    /// Maps the <c>/api/admin</c> group, which requires the <see cref="ApolloPolicies.Admin"/> policy (#30): anonymous
    /// callers get 401, logged-in users without it 403. <c>GET /api/admin/session</c> returns the admin's session; the
    /// admin pages (#31, #32, #33) add their endpoints to this group through <see cref="MapAdminGroup"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapAdminGroup();
        admin.MapGet("/session", (HttpContext http) => TypedResults.Ok(ApolloPrincipal.ToCurrentUser(http.User)));

        return endpoints;
    }

    /// <summary>The <c>/api/admin</c> route group with the <see cref="ApolloPolicies.Admin"/> policy.</summary>
    public static RouteGroupBuilder MapAdminGroup(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(GroupPath);
        group.RequireAuthorization(ApolloPolicies.Admin);
        return group;
    }
}
