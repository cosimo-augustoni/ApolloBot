using System.ComponentModel.DataAnnotations;

namespace Apollo.Web.Auth;

/// <summary>Settings of the web session's access checks (#30), bound from <c>Apollo:Web:Auth</c>.</summary>
public sealed class WebAuthOptions
{
    public const string SectionName = "Apollo:Web:Auth";

    /// <summary>
    /// How long the access claims in the session cookie are trusted before <see cref="AccessRevalidator"/> reads the
    /// synced guild state and the user's last logout again. A lost Administrator permission, or a logout on another
    /// device, takes effect within this time. Default 5 minutes; 1 second to 1 day.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00", ErrorMessage = $"{SectionName}:{nameof(RevalidationInterval)} must be between 00:00:01 and 1.00:00:00.")]
    public TimeSpan RevalidationInterval { get; set; } = TimeSpan.FromMinutes(5);
}
