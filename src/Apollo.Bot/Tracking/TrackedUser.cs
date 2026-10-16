using Apollo.Platform.Abstractions;

namespace Apollo.Bot.Tracking;

/// <summary>
/// A user whose tracking started or stopped, as <see cref="ITrackingLifecycleHandler"/> receives it.
/// </summary>
/// <param name="GuildId">The internal guild ID.</param>
/// <param name="UserId">The internal user ID.</param>
/// <param name="User">The user on the bot's platform, e.g. to look up their voice state.</param>
public sealed record TrackedUser(Guid GuildId, Guid UserId, PlatformRef User);
