using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Platform.Discord.Tests.Fakes;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.JsonModels;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordChatPlatformReadTests : IDisposable
{
    private static readonly PlatformRef _guild = Ref(DiscordHost.GuildId);

    private TestGateway? _gateway;

    [Fact]
    public async Task GetMemberAsync_WhenMemberIsCached_ShouldMapTheMember()
    {
        var joinedAt = new DateTimeOffset(2025, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var platform = Platform(new DiscordGuildBuilder()
            .WithRole(7, "Statistik")
            .WithMember(42, username: "alice", globalName: "Alice", nickname: "Ali", roles: [7], joinedAt: joinedAt, avatarHash: "abc"));

        var member = await platform.GetMemberAsync(_guild, Ref(42), TestContext.Current.CancellationToken);

        member.ShouldNotBeNull();
        member.User.ShouldBe(Ref(42));
        member.DisplayName.ShouldBe("Ali");
        member.AvatarUrl.ShouldNotBeNull().ShouldContain("/avatars/42/abc");
        member.IsBot.ShouldBeFalse();
        member.IsAdministrator.ShouldBeFalse();
        member.JoinedAt.ShouldBe(joinedAt);
        member.Roles.ShouldBe([Ref(7)]);
    }

    [Theory]
    [InlineData("alice", "Alice", null, "Alice")]
    [InlineData("alice", null, null, "alice")]
    public async Task GetMemberAsync_WhenMemberHasNoNickname_ShouldFallBackToGlobalNameThenUsername(
        string username, string? globalName, string? nickname, string expected)
    {
        var platform = Platform(new DiscordGuildBuilder().WithMember(42, username, globalName, nickname));

        var member = await platform.GetMemberAsync(_guild, Ref(42), TestContext.Current.CancellationToken);

        member.ShouldNotBeNull().DisplayName.ShouldBe(expected);
    }

    [Fact]
    public async Task GetMemberAsync_WhenMemberHasNoAvatar_ShouldReturnNoAvatarUrl()
    {
        var platform = Platform(new DiscordGuildBuilder().WithMember(42));

        var member = await platform.GetMemberAsync(_guild, Ref(42), TestContext.Current.CancellationToken);

        member.ShouldNotBeNull().AvatarUrl.ShouldBeNull();
    }

    [Fact]
    public async Task GetMemberAsync_WhenMemberIsABot_ShouldFlagIt()
    {
        var platform = Platform(new DiscordGuildBuilder().WithMember(42, bot: true));

        var member = await platform.GetMemberAsync(_guild, Ref(42), TestContext.Current.CancellationToken);

        member.ShouldNotBeNull().IsBot.ShouldBeTrue();
    }

    [Fact]
    public async Task GetMemberAsync_WhenUserIsNotCached_ShouldReturnNull()
    {
        var platform = Platform(new DiscordGuildBuilder().WithMember(42));

        var member = await platform.GetMemberAsync(_guild, Ref(43), TestContext.Current.CancellationToken);

        member.ShouldBeNull();
    }

    [Fact]
    public async Task GetMemberAsync_WhenGuildIsNotCachedYet_ShouldReturnNull()
    {
        var platform = Platform();

        var member = await platform.GetMemberAsync(_guild, Ref(42), TestContext.Current.CancellationToken);

        member.ShouldBeNull();
    }

    [Fact]
    public async Task GetMemberAsync_WhenGuildIsNotTheConfiguredGuild_ShouldThrow()
    {
        var otherGuild = new DiscordGuildBuilder(222222222222222222).WithMember(42);
        var platform = Platform(new DiscordGuildBuilder(), otherGuild);

        await Should.ThrowAsync<ArgumentException>(() => platform.GetMemberAsync(Ref(222222222222222222), Ref(42), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetMemberPermissionsAsync_WhenARoleGrantsAdministrator_ShouldReturnAdministrator()
    {
        var platform = Platform(new DiscordGuildBuilder()
            .WithRole(7, "Admin", permissions: Permissions.Administrator)
            .WithMember(42, roles: [7]));

        var permissions = await platform.GetMemberPermissionsAsync(_guild, Ref(42), TestContext.Current.CancellationToken);

        permissions.ShouldBe(PlatformPermissions.Administrator);
    }

    [Fact]
    public async Task GetMemberPermissionsAsync_WhenMemberOwnsTheGuild_ShouldReturnAdministrator()
    {
        var platform = Platform(new DiscordGuildBuilder().OwnedBy(42).WithMember(42));

        var permissions = await platform.GetMemberPermissionsAsync(_guild, Ref(42), TestContext.Current.CancellationToken);

        permissions.ShouldBe(PlatformPermissions.Administrator);
    }

    [Fact]
    public async Task GetMemberPermissionsAsync_WhenNoRoleGrantsAdministrator_ShouldReturnNone()
    {
        var platform = Platform(new DiscordGuildBuilder()
            .WithRole(7, "Mods", permissions: Permissions.ManageMessages)
            .WithMember(42, roles: [7]));

        var permissions = await platform.GetMemberPermissionsAsync(_guild, Ref(42), TestContext.Current.CancellationToken);

        permissions.ShouldBe(PlatformPermissions.None);
    }

    [Fact]
    public async Task GetMemberPermissionsAsync_WhenUserIsNotInTheGuild_ShouldReturnNone()
    {
        var platform = Platform(new DiscordGuildBuilder());

        var permissions = await platform.GetMemberPermissionsAsync(_guild, Ref(42), TestContext.Current.CancellationToken);

        permissions.ShouldBe(PlatformPermissions.None);
    }

    [Fact]
    public async Task GetMemberAsync_WhenARoleGrantsAdministrator_ShouldFlagTheMemberAsAdministrator()
    {
        var platform = Platform(new DiscordGuildBuilder()
            .WithRole(7, "Admin", permissions: Permissions.Administrator)
            .WithMember(42, roles: [7]));

        var member = await platform.GetMemberAsync(_guild, Ref(42), TestContext.Current.CancellationToken);

        member.ShouldNotBeNull().IsAdministrator.ShouldBeTrue();
    }

    [Fact]
    public async Task GetMemberRolesAsync_WhenMemberIsCached_ShouldReturnTheirRoles()
    {
        var platform = Platform(new DiscordGuildBuilder()
            .WithRole(7, "Statistik")
            .WithRole(8, "Mods")
            .WithMember(42, roles: [7, 8]));

        var roles = await platform.GetMemberRolesAsync(_guild, Ref(42), TestContext.Current.CancellationToken);

        roles.ShouldBe([Ref(7), Ref(8)], ignoreOrder: true);
    }

    [Fact]
    public async Task GetMemberRolesAsync_WhenUserIsNotInTheGuild_ShouldReturnEmpty()
    {
        var platform = Platform(new DiscordGuildBuilder());

        var roles = await platform.GetMemberRolesAsync(_guild, Ref(42), TestContext.Current.CancellationToken);

        roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListRolesAsync_WhenGuildIsCached_ShouldMapEveryRoleExceptEveryone()
    {
        var platform = Platform(new DiscordGuildBuilder()
            .WithRole(7, "Statistik", position: 2)
            .WithRole(8, "Admin", position: 3, permissions: Permissions.Administrator)
            .WithRole(9, "Apollo", position: 4, managed: true));

        var roles = await platform.ListRolesAsync(_guild, TestContext.Current.CancellationToken);

        roles.ShouldBe(
        [
            new PlatformRole(Ref(7), "Statistik", 2, IsAdministrator: false, IsManaged: false),
            new PlatformRole(Ref(8), "Admin", 3, IsAdministrator: true, IsManaged: false),
            new PlatformRole(Ref(9), "Apollo", 4, IsAdministrator: false, IsManaged: true),
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task ListChannelsAsync_WhenGuildIsCached_ShouldMapChannelsCategoriesAndThreads()
    {
        var platform = Platform(new DiscordGuildBuilder()
            .WithChannel(10, "Allgemein", ChannelType.CategoryChannel)
            .WithChannel(11, "chat", ChannelType.TextGuildChannel, parentId: 10)
            .WithChannel(12, "Lounge", ChannelType.VoiceGuildChannel, parentId: 10)
            .WithChannel(13, "news", ChannelType.AnnouncementGuildChannel)
            .WithChannel(14, "Bühne", ChannelType.StageGuildChannel)
            .WithChannel(15, "forum", ChannelType.ForumGuildChannel)
            .WithChannel(16, "thread", ChannelType.PublicGuildThread, parentId: 11)
            .WithChannel(17, "directory", ChannelType.DirectoryGuildChannel));

        var channels = await platform.ListChannelsAsync(_guild, TestContext.Current.CancellationToken);

        channels.ShouldBe(
        [
            new PlatformChannel(Ref(10), "Allgemein", PlatformChannelType.Category, Category: null, ThreadParent: null),
            new PlatformChannel(Ref(11), "chat", PlatformChannelType.Text, Category: Ref(10), ThreadParent: null),
            new PlatformChannel(Ref(12), "Lounge", PlatformChannelType.Voice, Category: Ref(10), ThreadParent: null),
            new PlatformChannel(Ref(13), "news", PlatformChannelType.Announcement, Category: null, ThreadParent: null),
            new PlatformChannel(Ref(14), "Bühne", PlatformChannelType.Stage, Category: null, ThreadParent: null),
            new PlatformChannel(Ref(15), "forum", PlatformChannelType.Forum, Category: null, ThreadParent: null),
            new PlatformChannel(Ref(16), "thread", PlatformChannelType.Thread, Category: null, ThreadParent: Ref(11)),
            new PlatformChannel(Ref(17), "directory", PlatformChannelType.Other, Category: null, ThreadParent: null),
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task ListMembersAsync_WhenGuildIsCached_ShouldMapEveryCachedMember()
    {
        var platform = Platform(new DiscordGuildBuilder().WithMember(42, "alice").WithMember(43, "bob"));

        var members = await platform.ListMembersAsync(_guild, TestContext.Current.CancellationToken);

        members.Select(m => (m.User, m.DisplayName)).ShouldBe([(Ref(42), "alice"), (Ref(43), "bob")], ignoreOrder: true);
    }

    [Fact]
    public async Task ListVoiceStatesAsync_WhenGuildIsCached_ShouldMapOneStatePerUserWithoutConnectionId()
    {
        var platform = Platform(new DiscordGuildBuilder()
            .WithMember(42)
            .WithMember(43)
            .WithChannel(12, "Lounge", ChannelType.VoiceGuildChannel)
            .WithVoiceState(42, channelId: 12, selfMute: true, selfDeaf: true)
            .WithVoiceState(43, channelId: 12, streaming: true, video: true));

        var voiceStates = await platform.ListVoiceStatesAsync(_guild, TestContext.Current.CancellationToken);

        voiceStates.ShouldBe(
        [
            new PlatformVoiceState(Ref(42), Ref(12), ConnectionId: null, SelfMute: true, SelfDeaf: true, Streaming: false, Video: false),
            new PlatformVoiceState(Ref(43), Ref(12), ConnectionId: null, SelfMute: false, SelfDeaf: false, Streaming: true, Video: true),
        ], ignoreOrder: true);
    }

    [Theory]
    [InlineData(UserStatusType.Online, PlatformPresenceStatus.Online)]
    [InlineData(UserStatusType.Idle, PlatformPresenceStatus.Idle)]
    [InlineData(UserStatusType.DoNotDisturb, PlatformPresenceStatus.DoNotDisturb)]
    [InlineData(UserStatusType.Invisible, PlatformPresenceStatus.Offline)]
    [InlineData(UserStatusType.Offline, PlatformPresenceStatus.Offline)]
    public async Task ListPresencesAsync_WhenGuildIsCached_ShouldMapTheStatus(UserStatusType status, PlatformPresenceStatus expected)
    {
        var platform = Platform(new DiscordGuildBuilder().WithMember(42).WithPresence(42, status));

        var presences = await platform.ListPresencesAsync(_guild, TestContext.Current.CancellationToken);

        var presence = presences.ShouldHaveSingleItem();
        presence.User.ShouldBe(Ref(42));
        presence.Status.ShouldBe(expected);
        presence.Activities.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListPresencesAsync_WhenMemberHasActivities_ShouldMapTypeNameAndStart()
    {
        var startedAt = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);
        var platform = Platform(new DiscordGuildBuilder()
            .WithMember(42)
            .WithPresence(42, UserStatusType.Online,
                new JsonUserActivity { Type = UserActivityType.Playing, Name = "Factorio", Timestamps = new() { StartTime = startedAt } },
                new JsonUserActivity { Type = UserActivityType.Listening, Name = "Spotify" }));

        var presences = await platform.ListPresencesAsync(_guild, TestContext.Current.CancellationToken);

        presences.ShouldHaveSingleItem().Activities.ShouldBe(
        [
            new PlatformActivity(PlatformActivityType.Playing, "Factorio", startedAt),
            new PlatformActivity(PlatformActivityType.Listening, "Spotify", null),
        ]);
    }

    [Fact]
    public async Task ListRolesAsync_WhenGuildIsNotCachedYet_ShouldReturnEmptyLists()
    {
        var platform = Platform();
        var ct = TestContext.Current.CancellationToken;

        (await platform.ListRolesAsync(_guild, ct)).ShouldBeEmpty();
        (await platform.ListChannelsAsync(_guild, ct)).ShouldBeEmpty();
        (await platform.ListMembersAsync(_guild, ct)).ShouldBeEmpty();
        (await platform.ListVoiceStatesAsync(_guild, ct)).ShouldBeEmpty();
        (await platform.ListPresencesAsync(_guild, ct)).ShouldBeEmpty();
    }

    [Fact]
    public void Capabilities_OnDiscord_ShouldSupportPresenceActivities()
    {
        var platform = Platform();

        platform.Platform.ShouldBe(PlatformKind.Discord);
        platform.Capabilities.PresenceActivities.ShouldBeTrue();
    }

    public void Dispose() => _gateway?.Dispose();

    private static PlatformRef Ref(ulong snowflake) => new(PlatformKind.Discord, snowflake.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private DiscordChatPlatform Platform(params DiscordGuildBuilder[] guilds)
    {
        _gateway = new TestGateway([.. guilds.Select(g => g.Build())]);
        return new DiscordChatPlatform(_gateway.Client, Options.Create(new DiscordPlatformOptions { Token = DiscordHost.Token, GuildId = DiscordHost.GuildId }));
    }
}
