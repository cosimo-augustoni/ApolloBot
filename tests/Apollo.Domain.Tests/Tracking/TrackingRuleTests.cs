using Apollo.Domain.Tracking;

namespace Apollo.Domain.Tests.Tracking;

public sealed class TrackingRuleTests
{
    private static readonly Guid _trackingRole = Guid.NewGuid();
    private static readonly Guid _otherRole = Guid.NewGuid();

    private static readonly TrackingSettings _noRole = new(TrackingRoleId: null, TrackingRoleDeleted: false);
    private static readonly TrackingSettings _roleConfigured = new(_trackingRole, TrackingRoleDeleted: false);
    private static readonly TrackingSettings _roleDeleted = new(_trackingRole, TrackingRoleDeleted: true);

    public static TheoryData<string> AllSettings => ["none", "configured", "deleted"];

    [Theory]
    [InlineData("none", TrackingMode.NoTrackingRole)]
    [InlineData("configured", TrackingMode.TrackingRole)]
    [InlineData("deleted", TrackingMode.TrackingRoleDeleted)]
    public void ModeOf_ShouldFollowTheTrackingRoleSetting(string settings, TrackingMode expected)
    {
        TrackingRule.ModeOf(Settings(settings)).ShouldBe(expected);
    }

    [Fact]
    public void IsTracked_WhenNoTrackingRoleIsConfigured_ShouldBeFalseForEveryMember()
    {
        foreach (var roles in new[] { Array.Empty<Guid>(), [_otherRole], [_trackingRole] })
        {
            TrackingRule.IsTracked(_noRole, Member(roles)).ShouldBeFalse();
        }
    }

    [Fact]
    public void IsTracked_WhenTrackingRoleIsConfiguredAndMemberHoldsIt_ShouldBeTrue()
    {
        TrackingRule.IsTracked(_roleConfigured, Member(_otherRole, _trackingRole)).ShouldBeTrue();
    }

    [Fact]
    public void IsTracked_WhenTrackingRoleIsConfiguredAndMemberDoesNotHoldIt_ShouldBeFalse()
    {
        TrackingRule.IsTracked(_roleConfigured, Member(_otherRole)).ShouldBeFalse();
    }

    [Fact]
    public void IsTracked_WhenTrackingRoleWasDeleted_ShouldBeFalseEvenForHolders()
    {
        TrackingRule.IsTracked(_roleDeleted, Member(_trackingRole)).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void IsTracked_WhenMemberLeftTheGuild_ShouldBeFalse(string settings)
    {
        var departed = new TrackingMember(IsCurrentMember: false, new HashSet<Guid> { _trackingRole });

        TrackingRule.IsTracked(Settings(settings), departed).ShouldBeFalse();
    }

    private static TrackingMember Member(params Guid[] roles) => new(IsCurrentMember: true, roles.ToHashSet());

    private static TrackingSettings Settings(string name) => name switch
    {
        "none" => _noRole,
        "configured" => _roleConfigured,
        "deleted" => _roleDeleted,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };
}
