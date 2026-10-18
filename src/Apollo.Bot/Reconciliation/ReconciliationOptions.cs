using System.ComponentModel.DataAnnotations;

namespace Apollo.Bot.Reconciliation;

/// <summary>
/// Settings shared by the periodic voice and presence reconciliation (#98), bound from <c>Apollo:Reconciliation</c>.
/// </summary>
public sealed class ReconciliationOptions
{
    public const string SectionName = "Apollo:Reconciliation";

    /// <summary>
    /// How often the bot compares its open voice and presence sessions with the current platform state, to correct a
    /// change it failed to store. Default 5 minutes; 10 seconds to 1 day.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:10", "1.00:00:00", ErrorMessage = $"{SectionName}:{nameof(Interval)} must be between 00:00:10 and 1.00:00:00.")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);
}
