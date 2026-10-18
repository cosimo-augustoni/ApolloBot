using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary>The server's activity for the admin dashboard (#37, <c>GET /api/admin/stats/server</c>).</summary>
public interface IServerStatsClient
{
    /// <summary>The activity of the configured server from <paramref name="from"/> to <paramref name="to"/> (Zurich days, both included).</summary>
    Task<ActivityStatsDto> GetAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
