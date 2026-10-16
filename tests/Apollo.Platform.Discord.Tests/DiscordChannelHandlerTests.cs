using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Platform.Discord.Tests.Fakes;
using Apollo.Testing;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordChannelHandlerTests
{
    private const ulong ForeignGuildId = 222222222222222222;

    private readonly MappingHarness _harness = new();

    [Fact]
    public async Task GuildChannelCreate_WhenChannelIsCreated_ShouldDispatchChannelCreatedWithItsCategory()
    {
        var channel = NetCordPayloads.Channel(DiscordHost.GuildId, 11, "chat", ChannelType.TextGuildChannel, parentId: 10);

        await ((IGuildChannelCreateGatewayHandler)Handler()).HandleAsync(channel);

        var created = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformChannelCreated>();
        created.OccurredAt.ShouldBe(TestTime.Now);
        created.Channel.ShouldBe(new PlatformChannel(Ref(11), "chat", PlatformChannelType.Text, Category: Ref(10), ThreadParent: null));
    }

    [Fact]
    public async Task GuildChannelUpdate_WhenChannelIsRenamed_ShouldDispatchChannelUpdated()
    {
        var channel = NetCordPayloads.Channel(DiscordHost.GuildId, 12, "Lounge 2", ChannelType.VoiceGuildChannel);

        await ((IGuildChannelUpdateGatewayHandler)Handler()).HandleAsync(channel);

        var updated = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformChannelUpdated>();
        updated.Channel.ShouldBe(new PlatformChannel(Ref(12), "Lounge 2", PlatformChannelType.Voice, Category: null, ThreadParent: null));
    }

    [Fact]
    public async Task GuildChannelDelete_WhenChannelIsDeleted_ShouldDispatchChannelDeleted()
    {
        var channel = NetCordPayloads.Channel(DiscordHost.GuildId, 11, "chat", ChannelType.TextGuildChannel);

        await ((IGuildChannelDeleteGatewayHandler)Handler()).HandleAsync(channel);

        _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformChannelDeleted>().Channel.ShouldBe(Ref(11));
    }

    [Fact]
    public async Task GuildChannelCreate_WhenChannelIsFromAnotherGuild_ShouldNotDispatch()
    {
        var channel = NetCordPayloads.Channel(ForeignGuildId, 11, "chat", ChannelType.TextGuildChannel);

        await ((IGuildChannelCreateGatewayHandler)Handler()).HandleAsync(channel);

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task GuildThreadCreate_WhenThreadIsCreated_ShouldDispatchChannelCreatedWithTheParent()
    {
        var thread = NetCordPayloads.Thread(DiscordHost.GuildId, 16, "thread", parentId: 11);

        await Handler().HandleAsync(new GuildThreadCreateEventArgs(thread, newlyCreated: true));

        var created = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformChannelCreated>();
        created.Channel.ShouldBe(new PlatformChannel(Ref(16), "thread", PlatformChannelType.Thread, Category: null, ThreadParent: Ref(11)));
    }

    [Fact]
    public async Task GuildThreadUpdate_WhenThreadIsRenamed_ShouldDispatchChannelUpdatedWithTheParent()
    {
        var thread = NetCordPayloads.Thread(DiscordHost.GuildId, 16, "renamed", parentId: 11);

        await Handler().HandleAsync(thread);

        var updated = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformChannelUpdated>();
        updated.Channel.ShouldBe(new PlatformChannel(Ref(16), "renamed", PlatformChannelType.Thread, Category: null, ThreadParent: Ref(11)));
    }

    [Fact]
    public async Task GuildThreadDelete_WhenThreadIsDeleted_ShouldDispatchChannelDeleted()
    {
        await Handler().HandleAsync(new GuildThreadDeleteEventArgs(16, DiscordHost.GuildId, 11, ChannelType.PublicGuildThread));

        _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformChannelDeleted>().Channel.ShouldBe(Ref(16));
    }

    [Fact]
    public async Task GuildThreadDelete_WhenThreadIsFromAnotherGuild_ShouldNotDispatch()
    {
        await Handler().HandleAsync(new GuildThreadDeleteEventArgs(16, ForeignGuildId, 11, ChannelType.PublicGuildThread));

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    private static PlatformRef Ref(ulong snowflake) => DiscordIds.ToRef(snowflake);

    private DiscordChannelHandler Handler() => new(_harness.Mapper, _harness.Time);
}
