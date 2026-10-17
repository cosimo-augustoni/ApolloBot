namespace Apollo.Data.Membership;

/// <summary>
/// What happened to the user's membership (<c>membership_events.kind</c>). A kick is a <see cref="Leave"/>; a ban of a
/// member is a <see cref="Ban"/> plus a <see cref="Leave"/>.
/// </summary>
public enum MembershipEventKind
{
    Join,
    Leave,
    Ban,
    Unban,
}
