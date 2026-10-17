using System.Text.Json.Serialization;

namespace Apollo.Web.Client.Contracts;

/// <summary>Whether a role was added to or removed from a setting (#33).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RoleSettingChangeAction>))]
public enum RoleSettingChangeAction
{
    [JsonStringEnumMemberName("added")]
    Added,

    [JsonStringEnumMemberName("removed")]
    Removed,
}
