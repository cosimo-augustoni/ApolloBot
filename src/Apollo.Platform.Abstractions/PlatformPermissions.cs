namespace Apollo.Platform.Abstractions;

/// <summary>
/// The guild permissions ApolloBot cares about, mapped from the platform's own permission bitfield by the adapter.
/// </summary>
[Flags]
public enum PlatformPermissions
{
    None = 0,

    /// <summary>
    /// Administrator: grants admin access (scope D8). It never makes a member tracked (scope §5.1.1).
    /// </summary>
    Administrator = 1 << 0,
}
