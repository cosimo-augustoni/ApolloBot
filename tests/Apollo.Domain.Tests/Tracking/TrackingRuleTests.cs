using Apollo.Domain.Tracking;

namespace Apollo.Domain.Tests.Tracking;

public sealed class TrackingRuleTests
{
    private static readonly Guid _trackingRole = Guid.NewGuid();
    private static readonly Guid _otherRole = Guid.NewGuid();

    private static readonly TrackingSettings _bootstrap = new(TrackingRoleId: null, TrackingRoleDeleted: false, TrackingRoleEverConfigured: false);
    private static readonly TrackingSettings _roleConfigured = new(_trackingRole, TrackingRoleDeleted: false, TrackingRoleEverConfigured: true);
    private static readonly TrackingSettings _roleDeleted = new(_trackingRole, TrackingRoleDeleted: true, TrackingRoleEverConfigured: true);
    private static readonly TrackingSettings _roleCleared = new(TrackingRoleId: null, TrackingRoleDeleted: false, TrackingRoleEverConfigured: true);

    public static TheoryData<string> AllSettings => ["bootstrap", "configured", "deleted", "cleared"];

    public static TheoryData<string> SettingsExceptBootstrap => ["configured", "deleted", "cleared"];

    [Theory]
    [InlineData("bootstrap", TrackingMode.Bootstrap)]
    [InlineData("configured", TrackingMode.TrackingRole)]
    [InlineData("deleted", TrackingMode.TrackingRoleDeleted)]
    [InlineData("cleared", TrackingMode.TrackingRoleCleared)]
    public void ModeOf_ShouldFollowTheTrackingRoleSetting(string settings, TrackingMode expected)
    {
        TrackingRule.ModeOf(Settings(settings)).ShouldBe(expected);
    }

    [Fact]
    public void IsTracked_WhenNoTrackingRoleWasEverConfiguredAndMemberIsAdministrator_ShouldBeTrue()
    {
        TrackingRule.IsTracked(_bootstrap, Member(isAdministrator: true)).ShouldBeTrue();
    }

    [Fact]
    public void IsTracked_WhenNoTrackingRoleWasEverConfiguredAndMemberIsNotAdministrator_ShouldBeFalse()
    {
        TrackingRule.IsTracked(_bootstrap, Member(isAdministrator: false, _otherRole)).ShouldBeFalse();
    }

    [Fact]
    public void IsTracked_WhenTrackingRoleIsConfiguredAndMemberHoldsIt_ShouldBeTrue()
    {
        TrackingRule.IsTracked(_roleConfigured, Member(isAdministrator: false, _otherRole, _trackingRole)).ShouldBeTrue();
    }

    [Fact]
    public void IsTracked_WhenTrackingRoleIsConfiguredAndMemberDoesNotHoldIt_ShouldBeFalse()
    {
        TrackingRule.IsTracked(_roleConfigured, Member(isAdministrator: false, _otherRole)).ShouldBeFalse();
    }

    [Fact]
    public void IsTracked_WhenTrackingRoleIsConfiguredAndAdministratorDoesNotHoldIt_ShouldBeFalse()
    {
        TrackingRule.IsTracked(_roleConfigured, Member(isAdministrator: true, _otherRole)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsTracked_WhenTrackingRoleWasDeleted_ShouldBeFalseEvenForHoldersAndAdministrators(bool isAdministrator)
    {
        TrackingRule.IsTracked(_roleDeleted, Member(isAdministrator, _trackingRole)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsTracked_WhenTrackingRoleWasCleared_ShouldBeFalseEvenForAdministrators(bool isAdministrator)
    {
        TrackingRule.IsTracked(_roleCleared, Member(isAdministrator, _trackingRole)).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void IsTracked_WhenMemberLeftTheGuild_ShouldBeFalse(string settings)
    {
        var departed = new TrackingMember(IsCurrentMember: false, IsAdministrator: true, new HashSet<Guid> { _trackingRole });

        TrackingRule.IsTracked(Settings(settings), departed).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(SettingsExceptBootstrap))]
    public void IsTracked_InEveryModeExceptBootstrap_ShouldGiveTheSameAnswerForAdministratorsAndOthers(string settings)
    {
        foreach (var roles in new[] { Array.Empty<Guid>(), [_otherRole], [_trackingRole] })
        {
            TrackingRule.IsTracked(Settings(settings), Member(isAdministrator: true, roles))
                .ShouldBe(TrackingRule.IsTracked(Settings(settings), Member(isAdministrator: false, roles)));
        }
    }

    private static TrackingMember Member(bool isAdministrator, params Guid[] roles) =>
        new(IsCurrentMember: true, isAdministrator, roles.ToHashSet());

    private static TrackingSettings Settings(string name) => name switch
    {
        "bootstrap" => _bootstrap,
        "configured" => _roleConfigured,
        "deleted" => _roleDeleted,
        "cleared" => _roleCleared,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };
}
