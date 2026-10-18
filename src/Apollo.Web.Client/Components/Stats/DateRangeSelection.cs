namespace Apollo.Web.Client.Components.Stats;

/// <summary>A range of <c>Europe/Zurich</c> days, both included, and the preset it came from (#37).</summary>
public sealed record DateRangeSelection(DateRangePreset Preset, DateOnly From, DateOnly To)
{
    /// <summary>Days the user chose (<see cref="DateRangePreset.Custom"/>).</summary>
    public static DateRangeSelection Custom(DateOnly from, DateOnly to) => new(DateRangePreset.Custom, from, to);
}
