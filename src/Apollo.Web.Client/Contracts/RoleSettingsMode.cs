using System.Text.Json.Serialization;

namespace Apollo.Web.Client.Contracts;

/// <summary>Who the bot tracks on a platform (scope §5.1.1), as the admin settings page shows it (#33).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RoleSettingsMode>))]
public enum RoleSettingsMode
{
    /// <summary>No tracking role is set: nobody is tracked, Administrators included.</summary>
    [JsonStringEnumMemberName("noTrackingRole")]
    NoTrackingRole,

    /// <summary>A tracking role is set and exists: exactly its holders are tracked.</summary>
    [JsonStringEnumMemberName("active")]
    Active,

    /// <summary>The tracking role was deleted on the platform: nobody is tracked.</summary>
    [JsonStringEnumMemberName("trackingRoleDeleted")]
    TrackingRoleDeleted,
}
