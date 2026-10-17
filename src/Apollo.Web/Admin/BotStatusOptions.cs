using System.ComponentModel.DataAnnotations;

namespace Apollo.Web.Admin;

/// <summary>Settings of the admin bot status page (#31), bound from <c>Apollo:Web:BotStatus</c>.</summary>
public sealed class BotStatusOptions
{
    public const string SectionName = "Apollo:Web:BotStatus";

    /// <summary>
    /// How old the latest heartbeat may be before the page warns that the bot no longer reports. The bot writes one
    /// every 30 s (#24). Default 2 minutes; 1 minute to 1 day.
    /// </summary>
    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00", ErrorMessage = $"{SectionName}:{nameof(StaleAfter)} must be between 00:01:00 and 1.00:00:00.")]
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(2);
}
