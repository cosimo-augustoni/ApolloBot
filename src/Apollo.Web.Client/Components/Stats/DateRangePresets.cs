using Apollo.Web.Client.Formatting;

namespace Apollo.Web.Client.Components.Stats;

/// <summary>
/// The ranges of the presets (#37): "Monat" is the current calendar month and "Jahr" the current calendar year, both up
/// to today in <c>Europe/Zurich</c> (D3), whatever the browser's time zone. They end at today because the server
/// averages divide by every day of the range, future days included (#35).
/// </summary>
public static class DateRangePresets
{
    /// <summary>The range a stats page opens with: <see cref="DateRangePreset.Month"/>.</summary>
    public static DateRangeSelection Default(TimeProvider time) => Resolve(DateRangePreset.Month, time);

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="preset"/> is <see cref="DateRangePreset.Custom"/>, which has no range of its own.</exception>
    public static DateRangeSelection Resolve(DateRangePreset preset, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var today = UiTime.DayOf(time.GetUtcNow());
        return preset switch
        {
            DateRangePreset.Month => new(preset, new DateOnly(today.Year, today.Month, 1), today),
            DateRangePreset.Year => new(preset, new DateOnly(today.Year, 1, 1), today),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Only a preset with a fixed range resolves."),
        };
    }
}
