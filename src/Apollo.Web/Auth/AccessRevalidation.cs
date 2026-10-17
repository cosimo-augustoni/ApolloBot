using System.Security.Claims;

namespace Apollo.Web.Auth;

/// <summary>The result of an access check; <see cref="Principal"/> is set only when it was <see cref="AccessRevalidationOutcome.Refreshed"/>.</summary>
public sealed record AccessRevalidation(AccessRevalidationOutcome Outcome, ClaimsPrincipal? Principal)
{
    public static AccessRevalidation Unchanged { get; } = new(AccessRevalidationOutcome.Unchanged, null);

    public static AccessRevalidation Rejected { get; } = new(AccessRevalidationOutcome.Rejected, null);

    public static AccessRevalidation Refreshed(ClaimsPrincipal principal) => new(AccessRevalidationOutcome.Refreshed, principal);
}
