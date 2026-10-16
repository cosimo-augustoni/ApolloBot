using Apollo.Platform.Discord.Tests.Fakes;
using Apollo.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordMemberDownloaderTests
{
    private readonly FakeGatewayCommands _commands = new();
    private readonly FakeTimeProvider _time = TestTime.CreateProvider();
    private readonly DiscordMemberDownloader _downloader;

    public DiscordMemberDownloaderTests()
    {
        _downloader = new DiscordMemberDownloader(_commands, _time);
    }

    [Fact]
    public async Task DownloadAsync_WhenCalled_ShouldRequestEveryMemberOfTheGuildWithANonce()
    {
        _commands.OnGuildUserRequest = request => _downloader.HandleAsync(
            NetCordPayloads.Chunk(DiscordHost.GuildId, request.Nonce, index: 0, count: 1));

        await _downloader.DownloadAsync(DiscordHost.GuildId, TestContext.Current.CancellationToken);

        var request = _commands.GuildUserRequests.ShouldHaveSingleItem();
        request.GuildId.ShouldBe(DiscordHost.GuildId);
        request.Query.ShouldBe(string.Empty);
        request.Limit.ShouldBe(0);
        request.Nonce.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task DownloadAsync_WhenAllChunksArrive_ShouldReturnTheMembersOfEveryChunk()
    {
        _commands.OnGuildUserRequest = async request =>
        {
            await _downloader.HandleAsync(NetCordPayloads.Chunk(DiscordHost.GuildId, request.Nonce, index: 1, count: 2, NetCordPayloads.JsonMember(43, "bob")));
            await _downloader.HandleAsync(NetCordPayloads.Chunk(DiscordHost.GuildId, request.Nonce, index: 0, count: 2, NetCordPayloads.JsonMember(42, "alice")));
        };

        var members = await _downloader.DownloadAsync(DiscordHost.GuildId, TestContext.Current.CancellationToken);

        members.Select(m => m.Id).ShouldBe([42UL, 43UL], ignoreOrder: true);
    }

    [Fact]
    public async Task DownloadAsync_WhenAChunkHasAnotherNonce_ShouldIgnoreIt()
    {
        _commands.OnGuildUserRequest = async request =>
        {
            await _downloader.HandleAsync(NetCordPayloads.Chunk(DiscordHost.GuildId, "someone-else", index: 0, count: 1, NetCordPayloads.JsonMember(99, "eve")));
            await _downloader.HandleAsync(NetCordPayloads.Chunk(DiscordHost.GuildId, request.Nonce, index: 0, count: 1, NetCordPayloads.JsonMember(42, "alice")));
        };

        var members = await _downloader.DownloadAsync(DiscordHost.GuildId, TestContext.Current.CancellationToken);

        members.ShouldHaveSingleItem().Id.ShouldBe(42UL);
    }

    [Fact]
    public async Task DownloadAsync_WhenTheLastChunkNeverArrives_ShouldTimeOut()
    {
        _commands.OnGuildUserRequest = request => _downloader.HandleAsync(
            NetCordPayloads.Chunk(DiscordHost.GuildId, request.Nonce, index: 0, count: 2, NetCordPayloads.JsonMember(42, "alice")));

        var download = _downloader.DownloadAsync(DiscordHost.GuildId, TestContext.Current.CancellationToken);
        _time.Advance(DiscordMemberDownloader.Timeout);

        await Should.ThrowAsync<TimeoutException>(download);
    }
}
