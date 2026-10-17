namespace Apollo.Web.Client.Api;

/// <summary>
/// The host answered a role settings write with 409: another admin changed the same settings at the same time. Nothing
/// of this write was kept; reload the settings.
/// </summary>
public sealed class RoleSettingsConflictException : Exception
{
    public RoleSettingsConflictException()
    {
    }

    public RoleSettingsConflictException(string message)
        : base(message)
    {
    }

    public RoleSettingsConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
