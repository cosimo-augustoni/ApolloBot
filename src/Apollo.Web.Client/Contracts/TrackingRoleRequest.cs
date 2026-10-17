namespace Apollo.Web.Client.Contracts;

/// <summary>Body of <c>PUT /api/admin/settings/{platform}/tracking-role</c> (#33).</summary>
/// <param name="RoleId">The new tracking role; <see langword="null"/> clears it, and then nobody is tracked.</param>
public sealed record TrackingRoleRequest(Guid? RoleId);
