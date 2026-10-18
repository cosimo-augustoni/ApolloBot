namespace Apollo.Web.Client.Components.Stats;

/// <summary>
/// Sums the hours of a weekday × hour heatmap (<c>ActivityStatsDto.Heatmap</c>: 7 rows of 24 hours) into blocks of
/// equal length, so a narrow screen can show 8 blocks of 3 hours instead of 24 cells too small to read.
/// </summary>
public static class HeatmapBlocks
{
    public const int HoursPerDay = 24;

    /// <summary>One list per weekday (in the heatmap's order), each with <c>24 / hoursPerBlock</c> blocks.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="hoursPerBlock"/> does not divide 24.</exception>
    public static IReadOnlyList<IReadOnlyList<HeatmapBlock>> Group(IReadOnlyList<IReadOnlyList<long>> heatmap, int hoursPerBlock)
    {
        ArgumentNullException.ThrowIfNull(heatmap);
        if (hoursPerBlock < 1 || HoursPerDay % hoursPerBlock != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursPerBlock), hoursPerBlock, "The blocks must divide the day.");
        }

        return
        [
            .. heatmap.Select(hours => (IReadOnlyList<HeatmapBlock>)
            [
                .. Enumerable.Range(0, HoursPerDay / hoursPerBlock)
                    .Select(block => block * hoursPerBlock)
                    .Select(start => new HeatmapBlock(start, hours.Skip(start).Take(hoursPerBlock).Sum())),
            ]),
        ];
    }
}
