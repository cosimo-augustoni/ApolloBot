using Apollo.Data.Tracking;
using Apollo.Web.Admin;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Tests.Admin;

public sealed class BotPermissionCheckTests
{
    private static readonly SettingsRole _statistik = new(Guid.NewGuid(), "Statistik", null, Position: 3, IsDeleted: false, MemberCount: 10);

    [Fact]
    public void BotPermissionCheck_WhenMissingManageRoles_ShouldWarn()
    {
        var check = BotPermissionCheck.Evaluate(new BotRoleFacts(ManagesRoles: false, HighestRolePosition: 5), _statistik);

        check.ShouldBe(new BotPermissionCheckDto(CanManageRoles: false, IsAboveTrackingRole: true));
        check.IsOk.ShouldBeFalse();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(null)]
    public void BotPermissionCheck_WhenBotRoleBelowTrackingRole_ShouldWarn(int? highestPosition)
    {
        var check = BotPermissionCheck.Evaluate(new BotRoleFacts(ManagesRoles: true, highestPosition), _statistik);

        check.ShouldBe(new BotPermissionCheckDto(CanManageRoles: true, IsAboveTrackingRole: false));
        check.IsOk.ShouldBeFalse();
    }

    [Fact]
    public void BotPermissionCheck_WhenOk_ShouldNotWarn()
    {
        var check = BotPermissionCheck.Evaluate(new BotRoleFacts(ManagesRoles: true, HighestRolePosition: 4), _statistik);

        check.ShouldBe(new BotPermissionCheckDto(CanManageRoles: true, IsAboveTrackingRole: true));
        check.IsOk.ShouldBeTrue();
    }

    [Fact]
    public void BotPermissionCheck_WhenNoTrackingRoleIsSet_ShouldCheckManageRolesOnly()
    {
        var check = BotPermissionCheck.Evaluate(new BotRoleFacts(ManagesRoles: true, HighestRolePosition: 1), trackingRole: null);

        check.ShouldBe(new BotPermissionCheckDto(CanManageRoles: true, IsAboveTrackingRole: null));
        check.IsOk.ShouldBeTrue();
    }

    [Fact]
    public void BotPermissionCheck_WhenTheTrackingRoleWasDeleted_ShouldCheckManageRolesOnly()
    {
        var check = BotPermissionCheck.Evaluate(new BotRoleFacts(ManagesRoles: true, HighestRolePosition: 1), _statistik with { IsDeleted = true });

        check.IsAboveTrackingRole.ShouldBeNull();
    }

    [Fact]
    public void BotPermissionCheck_WhenTheBotIsNotSynced_ShouldWarn()
    {
        var check = BotPermissionCheck.Evaluate(bot: null, _statistik);

        check.ShouldBe(new BotPermissionCheckDto(CanManageRoles: false, IsAboveTrackingRole: false));
    }
}
