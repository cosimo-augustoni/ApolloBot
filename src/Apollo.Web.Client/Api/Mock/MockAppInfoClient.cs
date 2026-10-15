using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api.Mock;

/// <summary><see cref="IAppInfoClient"/> with fixed data, for the prototype and bUnit tests (no HTTP).</summary>
public sealed class MockAppInfoClient : IAppInfoClient
{
    public static AppInfoDto AppInfo { get; } = new("0.0.0-mock", "Mock");

    public Task<AppInfoDto> GetAppInfoAsync(CancellationToken cancellationToken) => Task.FromResult(AppInfo);
}
