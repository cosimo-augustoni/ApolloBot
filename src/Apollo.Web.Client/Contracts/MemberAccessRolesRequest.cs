namespace Apollo.Web.Client.Contracts;

/// <summary>Body of <c>PUT /api/admin/settings/{platform}/member-access-roles</c> (#33): the complete new set.</summary>
public sealed record MemberAccessRolesRequest(IReadOnlyList<Guid> RoleIds);
