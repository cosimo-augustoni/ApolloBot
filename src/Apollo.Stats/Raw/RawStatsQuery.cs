namespace Apollo.Stats.Raw;

/// <summary>
/// What <see cref="IRawStatsService"/> reads: a guild, or one user in it, over the Zurich calendar days
/// <see cref="From"/> to <see cref="To"/>, both inclusive, as <c>ActivityStatsQuery</c> (#35).
/// </summary>
/// <param name="GuildId">The internal guild ID.</param>
/// <param name="UserId">The internal user ID, or <see langword="null"/> for the whole guild.</param>
/// <param name="From">The first Zurich day of the range.</param>
/// <param name="To">The last Zurich day of the range; must not be before <paramref name="From"/>.</param>
/// <param name="Top">How many entries a top list returns.</param>
/// <param name="IncludeBots">Whether bots count; they are left out by default (scope §5.1).</param>
public sealed record RawStatsQuery(Guid GuildId, Guid? UserId, DateOnly From, DateOnly To, int Top = RawStatsQuery.DefaultTop, bool IncludeBots = false)
{
    public const int DefaultTop = 5;
}
