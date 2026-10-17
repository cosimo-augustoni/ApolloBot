using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary>The bot status of the admin page (#31, <c>GET /api/admin/bot-status</c>).</summary>
public interface IBotStatusClient
{
    Task<BotStatusDto> GetStatusAsync(CancellationToken cancellationToken);
}
