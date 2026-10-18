using System.ComponentModel.DataAnnotations;

namespace Apollo.Bot.Voice;

/// <summary>
/// Settings of the voice reconciliation (#98), bound from <c>Apollo:Voice:Reconciliation</c>. Its interval is shared with
/// presence (<see cref="Reconciliation.ReconciliationOptions"/>).
/// </summary>
public sealed class VoiceReconciliationOptions
{
    public const string SectionName = "Apollo:Voice:Reconciliation";

    /// <summary>
    /// The longest blind spot (a restart or a gateway disconnect) after which a member still in the same channel keeps
    /// their session; after a longer one it ends at the last moment the bot knew it and a new one starts. Default
    /// 2 minutes; 0 to 1 day.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "1.00:00:00", ErrorMessage = $"{SectionName}:{nameof(MaxGap)} must be between 00:00:00 and 1.00:00:00.")]
    public TimeSpan MaxGap { get; set; } = TimeSpan.FromMinutes(2);
}
