using Apollo.Domain.Guilds;

namespace Apollo.Domain.Tests.Guilds;

public sealed class MemberAdministratorTests
{
    [Fact]
    public void IsAdministrator_WhenARoleGrantsAdministrator_ShouldBeTrue()
    {
        MemberAdministrator.IsAdministrator(isOwner: false, heldRolesGrantAdministrator: [false, true]).ShouldBeTrue();
    }

    [Fact]
    public void IsAdministrator_WhenMemberOwnsTheGuild_ShouldBeTrueWithoutAnyRole()
    {
        MemberAdministrator.IsAdministrator(isOwner: true, heldRolesGrantAdministrator: []).ShouldBeTrue();
    }

    [Fact]
    public void IsAdministrator_WhenNoRoleGrantsAdministratorAndMemberIsNotOwner_ShouldBeFalse()
    {
        MemberAdministrator.IsAdministrator(isOwner: false, heldRolesGrantAdministrator: [false, false]).ShouldBeFalse();
    }

    [Fact]
    public void IsAdministrator_WhenMemberHoldsNoRole_ShouldBeFalse()
    {
        MemberAdministrator.IsAdministrator(isOwner: false, heldRolesGrantAdministrator: []).ShouldBeFalse();
    }
}
