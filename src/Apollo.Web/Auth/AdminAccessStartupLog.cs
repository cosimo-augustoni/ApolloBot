using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Identity;
using Microsoft.Extensions.Options;

namespace Apollo.Web.Auth;

/// <summary>
/// Logs at startup who gets admin access (#30): the Administrator permission in the configured guild of each login
/// platform, nothing else (D8). There is no setting to change it, so the log is where an operator sees the rule.
/// </summary>
public static partial class AdminAccessStartupLog
{
    public static void Write(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AdminAccessStartupLog).FullName!);
        var interval = services.GetRequiredService<IOptions<WebAuthOptions>>().Value.RevalidationInterval;
        foreach (var provider in services.GetServices<IPlatformIdentityProvider>())
        {
            LogAdminAccess(logger, provider.Platform, provider.Guild.ExternalId, interval);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Admin access on {Platform}: the Administrator permission in guild {GuildId}, nothing else; access is re-checked every {RevalidationInterval}")]
    private static partial void LogAdminAccess(ILogger logger, PlatformKind platform, string guildId, TimeSpan revalidationInterval);
}
