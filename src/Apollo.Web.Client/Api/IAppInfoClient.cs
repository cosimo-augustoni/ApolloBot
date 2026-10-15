using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary>Reads build and environment information from the web host.</summary>
public interface IAppInfoClient
{
    Task<AppInfoDto> GetAppInfoAsync(CancellationToken cancellationToken);
}
