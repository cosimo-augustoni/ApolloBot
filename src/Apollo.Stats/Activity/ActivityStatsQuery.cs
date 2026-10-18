namespace Apollo.Stats.Activity;

/// <summary>
/// What <see cref="IActivityStatsService"/> computes: the activity of a guild, or of one user in it, over the Zurich
/// calendar days <see cref="From"/> to <see cref="To"/>, both inclusive (scope §5.3, D3).
/// </summary>
/// <param name="GuildId">The internal guild ID.</param>
/// <param name="UserId">The internal user ID, or <see langword="null"/> for the whole guild.</param>
/// <param name="From">The first Zurich day of the range.</param>
/// <param name="To">The last Zurich day of the range; must not be before <paramref name="From"/>.</param>
/// <param name="IncludeBots">Whether bots count; they are left out by default (scope §5.1).</param>
public sealed record ActivityStatsQuery(Guid GuildId, Guid? UserId, DateOnly From, DateOnly To, bool IncludeBots = false);
