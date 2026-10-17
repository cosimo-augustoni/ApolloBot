namespace Apollo.Web.Client.Api;

/// <summary>
/// The host answered a role settings write with 400: a role was deleted on the platform in the meantime, or does not
/// belong to the guild and platform. Nothing was changed.
/// </summary>
public sealed class RoleSettingsRejectedException : Exception
{
    public RoleSettingsRejectedException()
    {
    }

    public RoleSettingsRejectedException(string message)
        : base(message)
    {
    }

    public RoleSettingsRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
