using Apollo.Domain.Tracking;

namespace Apollo.Data.Tracking;

/// <summary>Current, non-bot members of one <c>(guild, platform)</c>, split by <see cref="TrackingRule"/> (#32).</summary>
/// <param name="Tracked">Members the rule tracks.</param>
/// <param name="Untracked">The other members.</param>
/// <param name="Mode">Who is tracked, as the bot's gate decides it.</param>
public sealed record MemberTracking(int Tracked, int Untracked, TrackingMode Mode);
