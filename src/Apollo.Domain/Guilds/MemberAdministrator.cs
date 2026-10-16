namespace Apollo.Domain.Guilds;

/// <summary>
/// Whether a member has the Administrator permission (scope D8, §5.1.1). Computed by ApolloBot itself from the roles
/// it synced, rather than taken from a platform library cache (docs/platform-parity.md).
/// </summary>
public static class MemberAdministrator
{
    /// <param name="isOwner">Whether the member owns the guild; the owner holds every permission.</param>
    /// <param name="heldRolesGrantAdministrator">For each role the member holds, whether it grants Administrator.</param>
    public static bool IsAdministrator(bool isOwner, IEnumerable<bool> heldRolesGrantAdministrator) =>
        isOwner || heldRolesGrantAdministrator.Any(grantsAdministrator => grantsAdministrator);
}
