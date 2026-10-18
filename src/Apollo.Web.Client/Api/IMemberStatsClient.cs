using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary>One member's stats for the admin member page (#38, <c>GET /api/admin/members/{userId}/stats</c>).</summary>
public interface IMemberStatsClient
{
    /// <summary>
    /// The stats of <paramref name="userId"/> (internal ID) from <paramref name="from"/> to <paramref name="to"/>
    /// (Zurich days, both included); <see langword="null"/> when the user is no member of the server.
    /// </summary>
    Task<MemberStatsDto?> GetAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
