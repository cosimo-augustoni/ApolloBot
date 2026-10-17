namespace Apollo.Web.Auth;

/// <summary>What <see cref="AccessRevalidator.RevalidateAsync"/> decided about a session principal.</summary>
public enum AccessRevalidationOutcome
{
    /// <summary>Checked recently enough: the claims stay as they are, no database lookup was made.</summary>
    Unchanged,

    /// <summary>Checked now: <see cref="AccessRevalidation.Principal"/> replaces the session principal.</summary>
    Refreshed,

    /// <summary>The session must end: the user logged out after it started, or it is not a session principal.</summary>
    Rejected,
}
