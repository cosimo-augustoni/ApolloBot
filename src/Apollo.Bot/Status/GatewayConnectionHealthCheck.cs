using Apollo.Platform.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Apollo.Bot.Status;

/// <summary>
/// Unhealthy once the gateway has been disconnected for longer than <see cref="GracePeriod"/>; degraded while the
/// adapter reconnects (or first connects) within it.
/// </summary>
internal sealed class GatewayConnectionHealthCheck(IPlatformConnectionStatus connection, TimeProvider timeProvider) : IHealthCheck
{
    public const string Name = "gateway";

    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(2);

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var current = connection.Current;
        if (current.IsConnected)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Gateway connected."));
        }

        var disconnectedFor = timeProvider.GetUtcNow() - current.StateChangedAt;
        var description = $"Gateway disconnected since {current.StateChangedAt:O}.";
        return Task.FromResult(disconnectedFor > GracePeriod
            ? HealthCheckResult.Unhealthy(description)
            : HealthCheckResult.Degraded(description));
    }
}
