using Apollo.Data.Identity;

namespace Apollo.Data.Sessions;

/// <summary>
/// When a <see cref="User"/> last logged out of the website (#30). Session cookies issued before this time are
/// rejected at their next access check, so a copied cookie does not outlive the logout. One row per user, updated in
/// place.
/// </summary>
public sealed class UserSignOut
{
    public Guid UserId { get; init; }

    public DateTimeOffset SignedOutAt { get; set; }
}
