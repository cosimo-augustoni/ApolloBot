using System.Text.Json.Serialization;

namespace Apollo.Web.Client.Contracts;

/// <summary>Which role setting a history entry is about (#33).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RoleSettingKind>))]
public enum RoleSettingKind
{
    [JsonStringEnumMemberName("tracking")]
    Tracking,

    [JsonStringEnumMemberName("memberAccess")]
    MemberAccess,
}
