namespace Apollo.Web.Client.Components.Stats;

/// <summary>How a stats page picks its range (#37): a preset ending today, or days the user chose.</summary>
public enum DateRangePreset
{
    Month,
    Year,
    Custom,
}
