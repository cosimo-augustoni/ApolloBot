using System.Collections.Concurrent;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Apollo.Bot.Status;

/// <summary>
/// The bot is a worker without a health endpoint, so this publisher is what runs its health checks
/// (<c>HealthCheckPublisherHostedService</c>, every 30 s). It logs each check's first status, then only real
/// transitions, so an unhealthy gateway shows up in the logs and the Aspire dashboard.
/// </summary>
internal sealed partial class HealthStatusLogPublisher(ILogger<HealthStatusLogPublisher> logger) : IHealthCheckPublisher
{
    private readonly ConcurrentDictionary<string, HealthStatus> _lastStatus = new();

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        foreach (var (check, entry) in report.Entries)
        {
            if (!_lastStatus.TryGetValue(check, out var previous))
            {
                // No history: the gateway check starts Degraded until the first connect, which is not a change.
                LogInitial(entry.Status == HealthStatus.Unhealthy ? LogLevel.Error : LogLevel.Information, check, entry.Status, entry.Description);
            }
            else if (previous != entry.Status)
            {
                LogTransition(LevelOf(entry.Status), check, previous, entry.Status, entry.Description);
            }

            _lastStatus[check] = entry.Status;
        }

        return Task.CompletedTask;
    }

    private static LogLevel LevelOf(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => LogLevel.Information,
        HealthStatus.Degraded => LogLevel.Warning,
        _ => LogLevel.Error,
    };

    [LoggerMessage(Message = "Health check {Check} is {Status}: {Description}")]
    private partial void LogInitial(LogLevel level, string check, HealthStatus status, string? description);

    [LoggerMessage(Message = "Health check {Check} changed from {PreviousStatus} to {Status}: {Description}")]
    private partial void LogTransition(LogLevel level, string check, HealthStatus previousStatus, HealthStatus status, string? description);
}
