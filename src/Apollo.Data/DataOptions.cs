using System.ComponentModel.DataAnnotations;

namespace Apollo.Data;

/// <summary>
/// Settings under <c>Apollo:Data</c>.
/// </summary>
public sealed class DataOptions
{
    public const string SectionName = "Apollo:Data";

    /// <summary>
    /// How long bot and web wait at startup for the MigrationService outside Development (Swarm has no start order).
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan MigrationWaitTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
