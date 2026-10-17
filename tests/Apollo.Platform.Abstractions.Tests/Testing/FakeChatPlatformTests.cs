using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Apollo.Testing;
using Apollo.Testing.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Platform.Abstractions.Tests.Testing;

public sealed class FakeChatPlatformTests
{
    private static readonly PlatformRef _guild = TestPlatform.Guild;
    private static readonly PlatformRef _alice = TestPlatform.User("alice");

    private readonly List<PlatformEvent> _received = [];
    private readonly FakeChatPlatform _platform;

    public FakeChatPlatformTests()
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(_received)
            .AddPlatformEventDispatcher()
            .AddPlatformEventHandler<PlatformMemberUpdated, RecordingHandler<PlatformMemberUpdated>>()
            .AddPlatformEventHandler<PlatformMessageCreated, RecordingHandler<PlatformMessageCreated>>()
            .BuildServiceProvider();
        _platform = new FakeChatPlatform(provider.GetRequiredService<IPlatformEventDispatcher>(), TestTime.CreateProvider());
    }

    [Fact]
    public async Task GetMemberRolesAsync_WhenMemberHoldsRole_ShouldReturnRole()
    {
        var role = TestPlatform.Role("stats");
        _platform.WithRole(new PlatformRoleBuilder().WithRef(role).Build())
            .WithMember(new PlatformMemberBuilder().ForUser(_alice).WithRoles(role).Build());

        var roles = await _platform.GetMemberRolesAsync(_guild, _alice, TestContext.Current.CancellationToken);

        roles.ShouldBe([role]);
    }

    [Fact]
    public async Task GetMemberRolesAsync_WhenMemberIsUnknown_ShouldReturnEmpty()
    {
        var roles = await _platform.GetMemberRolesAsync(_guild, _alice, TestContext.Current.CancellationToken);

        roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMemberAsync_WhenMemberIsSeeded_ShouldReturnMember()
    {
        var member = new PlatformMemberBuilder().ForUser(_alice).Build();
        _platform.WithMember(member);

        var result = await _platform.GetMemberAsync(_guild, _alice, TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(member);
    }

    [Fact]
    public async Task GetMemberPermissionsAsync_WhenMemberIsAdministrator_ShouldReturnAdministrator()
    {
        _platform.WithMember(new PlatformMemberBuilder().ForUser(_alice).AsAdministrator().Build());

        var permissions = await _platform.GetMemberPermissionsAsync(_guild, _alice, TestContext.Current.CancellationToken);

        permissions.ShouldBe(PlatformPermissions.Administrator);
    }

    [Fact]
    public async Task GetMemberPermissionsAsync_WhenMemberIsNotAdministrator_ShouldReturnNone()
    {
        _platform.WithMember(new PlatformMemberBuilder().ForUser(_alice).Build());

        var permissions = await _platform.GetMemberPermissionsAsync(_guild, _alice, TestContext.Current.CancellationToken);

        permissions.ShouldBe(PlatformPermissions.None);
    }

    [Fact]
    public async Task ListMembersAsync_WhenListingsAreSetToFail_ShouldThrowThatManyTimesThenReturnMembers()
    {
        var member = new PlatformMemberBuilder().Build();
        _platform.WithMember(member).FailMemberListings(2);
        var cancellationToken = TestContext.Current.CancellationToken;

        await Should.ThrowAsync<TimeoutException>(() => _platform.ListMembersAsync(_guild, cancellationToken));
        await Should.ThrowAsync<TimeoutException>(() => _platform.ListMembersAsync(_guild, cancellationToken));
        (await _platform.ListMembersAsync(_guild, cancellationToken)).ShouldBe([member]);
        _platform.MemberListings.ShouldBe(3);
    }

    [Fact]
    public async Task ListAsync_WhenStateIsSeeded_ShouldReturnSeededState()
    {
        var role = new PlatformRoleBuilder().Build();
        var channel = new PlatformChannelBuilder().Build();
        var member = new PlatformMemberBuilder().Build();
        var firstConnection = new PlatformVoiceStateBuilder().WithConnection("a").Build();
        var secondConnection = new PlatformVoiceStateBuilder().WithConnection("b").Build();
        var presence = new PlatformPresenceBuilder().Build();
        _platform.WithRole(role).WithChannel(channel).WithMember(member)
            .WithVoiceState(firstConnection).WithVoiceState(secondConnection).WithPresence(presence);
        var cancellationToken = TestContext.Current.CancellationToken;

        (await _platform.ListRolesAsync(_guild, cancellationToken)).ShouldBe([role]);
        (await _platform.ListChannelsAsync(_guild, cancellationToken)).ShouldBe([channel]);
        (await _platform.ListMembersAsync(_guild, cancellationToken)).ShouldBe([member]);
        (await _platform.ListVoiceStatesAsync(_guild, cancellationToken)).ShouldBe([firstConnection, secondConnection]);
        (await _platform.ListPresencesAsync(_guild, cancellationToken)).ShouldBe([presence]);
    }

    [Fact]
    public async Task ListRolesAsync_WhenGuildIsUnknown_ShouldThrow()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _platform.ListRolesAsync(TestPlatform.Guild with { ExternalId = "other" }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddRoleAsync_WhenCalled_ShouldRecordCallUpdateRolesAndEchoMemberUpdated()
    {
        var role = TestPlatform.Role("stats");
        _platform.WithRole(new PlatformRoleBuilder().WithRef(role).Build())
            .WithMember(new PlatformMemberBuilder().ForUser(_alice).Build());

        await _platform.AddRoleAsync(_guild, _alice, role, TestContext.Current.CancellationToken);

        _platform.RoleChanges.ShouldBe([new FakeRoleChange(_alice, role, FakeRoleChangeKind.Added)]);
        (await _platform.GetMemberRolesAsync(_guild, _alice, TestContext.Current.CancellationToken)).ShouldBe([role]);
        var echo = _received.ShouldHaveSingleItem().ShouldBeOfType<PlatformMemberUpdated>();
        echo.Guild.ShouldBe(_guild);
        echo.OccurredAt.ShouldBe(TestTime.Now);
        echo.Member.User.ShouldBe(_alice);
        echo.Member.Roles.ShouldBe([role]);
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenCalled_ShouldRecordCallUpdateRolesAndEchoMemberUpdated()
    {
        var role = TestPlatform.Role("stats");
        _platform.WithRole(new PlatformRoleBuilder().WithRef(role).Build())
            .WithMember(new PlatformMemberBuilder().ForUser(_alice).WithRoles(role).Build());

        await _platform.RemoveRoleAsync(_guild, _alice, role, TestContext.Current.CancellationToken);

        _platform.RoleChanges.ShouldBe([new FakeRoleChange(_alice, role, FakeRoleChangeKind.Removed)]);
        (await _platform.GetMemberRolesAsync(_guild, _alice, TestContext.Current.CancellationToken)).ShouldBeEmpty();
        var echo = _received.ShouldHaveSingleItem().ShouldBeOfType<PlatformMemberUpdated>();
        echo.Member.User.ShouldBe(_alice);
        echo.Member.Roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddRoleAsync_WhenMemberIsUnknown_ShouldThrow()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _platform.AddRoleAsync(_guild, _alice, TestPlatform.Role("stats"), TestContext.Current.CancellationToken));

        _platform.RoleChanges.ShouldBeEmpty();
    }

    [Fact]
    public async Task RoleChanges_WhenSetToFail_ShouldThrowTheFailureAndChangeNothing()
    {
        var role = TestPlatform.Role("stats");
        var failure = new InvalidOperationException("Missing Permissions");
        _platform.WithMember(new PlatformMemberBuilder().ForUser(_alice).WithRoles(role).Build()).FailRoleChanges(failure);

        (await Should.ThrowAsync<InvalidOperationException>(
            () => _platform.AddRoleAsync(_guild, _alice, TestPlatform.Role("other"), TestContext.Current.CancellationToken))).ShouldBeSameAs(failure);
        (await Should.ThrowAsync<InvalidOperationException>(
            () => _platform.RemoveRoleAsync(_guild, _alice, role, TestContext.Current.CancellationToken))).ShouldBeSameAs(failure);

        _platform.RoleChanges.ShouldBeEmpty();
        (await _platform.GetMemberRolesAsync(_guild, _alice, TestContext.Current.CancellationToken)).ShouldBe([role]);
        _received.ShouldBeEmpty();
    }

    [Fact]
    public async Task RaiseAsync_WhenCalled_ShouldDeliverEventToRegisteredHandlers()
    {
        var message = new PlatformMessageCreatedBuilder().Build();

        await _platform.RaiseAsync(message, TestContext.Current.CancellationToken);

        _received.ShouldBe([message]);
    }

    [Fact]
    public void Capabilities_ByDefault_ShouldSupportPresenceActivities()
    {
        _platform.Capabilities.PresenceActivities.ShouldBeTrue();
    }

    [Fact]
    public void Capabilities_WhenPresenceActivitiesAreTurnedOff_ShouldReportUnsupported()
    {
        _platform.WithCapabilities(new PlatformCapabilities(PresenceActivities: false));

        _platform.Capabilities.PresenceActivities.ShouldBeFalse();
    }

    [Fact]
    public async Task ListPresencesAsync_WhenPresenceActivitiesAreUnsupported_ShouldReturnPresencesWithoutActivities()
    {
        _platform.WithCapabilities(new PlatformCapabilities(PresenceActivities: false))
            .WithPresence(new PlatformPresenceBuilder().Playing("Factorio").Build());

        var presences = await _platform.ListPresencesAsync(_guild, TestContext.Current.CancellationToken);

        presences.ShouldHaveSingleItem().Activities.ShouldBeEmpty();
    }

    private sealed class RecordingHandler<TEvent>(List<PlatformEvent> received) : IPlatformEventHandler<TEvent>
        where TEvent : PlatformEvent
    {
        public Task HandleAsync(TEvent platformEvent, CancellationToken cancellationToken)
        {
            received.Add(platformEvent);
            return Task.CompletedTask;
        }
    }
}
