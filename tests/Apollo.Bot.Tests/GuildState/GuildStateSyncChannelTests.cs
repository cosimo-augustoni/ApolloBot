using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;

namespace Apollo.Bot.Tests.GuildState;

public sealed class GuildStateSyncChannelTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly PlatformRef _category = TestPlatform.Channel("category");
    private static readonly PlatformRef _chat = TestPlatform.Channel("chat");

    [Fact]
    public async Task ChannelCreated_WhenChannelIsNew_ShouldStoreItWithItsCategory()
    {
        await using var host = await SyncedHostAsync();

        await host.RaiseAsync(new PlatformChannelCreatedBuilder()
            .WithChannel(new PlatformChannelBuilder().WithRef(TestPlatform.Channel("news")).Named("news").OfType(PlatformChannelType.Announcement).InCategory(_category).Build())
            .Build());

        (await GuildStateView.LoadAsync(Fixture)).Channels["news"]
            .ShouldBe(new GuildStateView.ChannelRow("news", PlatformChannelType.Announcement, "category", null, null));
    }

    [Fact]
    public async Task ChannelCreated_WhenChannelIsAThread_ShouldReferenceTheParentChannel()
    {
        await using var host = await SyncedHostAsync();

        await host.RaiseAsync(new PlatformChannelCreatedBuilder()
            .WithChannel(new PlatformChannelBuilder().WithRef(TestPlatform.Channel("thread")).Named("thread").AsThreadOf(_chat).Build())
            .Build());

        (await GuildStateView.LoadAsync(Fixture)).Channels["thread"].ThreadParent.ShouldBe("chat");
    }

    [Fact]
    public async Task ChannelUpdated_WhenChannelIsRenamedAndMoved_ShouldUpdateTheRow()
    {
        await using var host = await SyncedHostAsync();

        await host.RaiseAsync(new PlatformChannelUpdatedBuilder()
            .WithChannel(new PlatformChannelBuilder().WithRef(_chat).Named("plaudern").Build())
            .Build());

        (await GuildStateView.LoadAsync(Fixture)).Channels["chat"]
            .ShouldBe(new GuildStateView.ChannelRow("plaudern", PlatformChannelType.Text, null, null, null));
    }

    [Fact]
    public async Task ChannelDeleted_WhenProcessed_ShouldSetDeletedAtAndKeepTheRow()
    {
        await using var host = await SyncedHostAsync();
        var deletedAt = TestTime.Now.AddHours(1);

        await host.RaiseAsync(new PlatformChannelDeletedBuilder().ForChannel(_chat).At(deletedAt).Build());

        var chat = (await GuildStateView.LoadAsync(Fixture)).Channels["chat"];
        chat.DeletedAt.ShouldBe(deletedAt);
        chat.Name.ShouldBe("chat");
    }

    private async Task<SyncHost> SyncedHostAsync()
    {
        var host = new SyncHost(Fixture);
        await host.SyncAsync(new PlatformGuildAvailableBuilder()
            .WithChannels(
                new PlatformChannelBuilder().WithRef(_category).Named("Allgemein").OfType(PlatformChannelType.Category).Build(),
                new PlatformChannelBuilder().WithRef(_chat).Named("chat").InCategory(_category).Build())
            .Build());
        return host;
    }
}
