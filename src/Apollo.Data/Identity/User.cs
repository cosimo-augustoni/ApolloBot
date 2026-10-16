namespace Apollo.Data.Identity;

/// <summary>
/// The internal identity of a user. Platform IDs map to it through <see cref="UserPlatformId"/> (scope §4.2).
/// </summary>
public sealed class User
{
    public Guid Id { get; init; }
}
