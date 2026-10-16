using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions;
using Apollo.Testing.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.IntegrationTests.Data.Identity;

public sealed class PlatformIdResolverTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly PlatformRef _discordRef = new(PlatformKind.Discord, "1234567890");
    private static readonly PlatformRef _fluxerRef = new(PlatformKind.Fluxer, "1234567890");

    public static TheoryData<string> Targets => IdentityTarget.All;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(Targets))]
    public async Task Resolve_WhenRefIsUnknown_ShouldCreateOneRowAndOneMappingAndReturnNewId(string name)
    {
        var target = IdentityTarget.Named(name);
        await using var db = Fixture.CreateDbContext();

        var id = await target.Resolve(CreateResolver(db), _discordRef, Ct);

        id.ShouldNotBe(Guid.Empty);
        (await target.ReadIdsAsync(db)).ShouldBe([id]);
        (await target.ReadMappedIdsAsync(db)).ShouldBe([id]);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public async Task Resolve_WhenRefIsKnown_ShouldReturnSameIdAndAddNoRows(string name)
    {
        var target = IdentityTarget.Named(name);
        Guid first;
        await using (var firstDb = Fixture.CreateDbContext())
        {
            first = await target.Resolve(CreateResolver(firstDb), _discordRef, Ct);
        }

        await using var db = Fixture.CreateDbContext();

        var second = await target.Resolve(CreateResolver(db), _discordRef, Ct);

        second.ShouldBe(first);
        (await target.ReadIdsAsync(db)).ShouldBe([first]);
        (await target.ReadMappedIdsAsync(db)).ShouldBe([first]);
    }

    // Every call gets its own context and its own cache, like separate processes (scope §4.1): only the database can
    // keep them from creating duplicates.
    [Theory]
    [MemberData(nameof(Targets))]
    public async Task Resolve_WhenCalledConcurrentlyForUnknownRef_ShouldCreateExactlyOneRowAndReturnOneId(string name)
    {
        var target = IdentityTarget.Named(name);

        var ids = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await using var callDb = Fixture.CreateDbContext();
            return await target.Resolve(CreateResolver(callDb), _discordRef, Ct);
        }, Ct)));

        await using var db = Fixture.CreateDbContext();
        ids.Distinct().ShouldHaveSingleItem();
        (await target.ReadIdsAsync(db)).ShouldBe([ids[0]]);
        (await target.ReadMappedIdsAsync(db)).ShouldBe([ids[0]]);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public async Task Resolve_WhenSameExternalIdOnDiscordAndFluxer_ShouldReturnDifferentIds(string name)
    {
        var target = IdentityTarget.Named(name);
        await using var db = Fixture.CreateDbContext();
        var resolver = CreateResolver(db);

        var discordId = await target.Resolve(resolver, _discordRef, Ct);
        var fluxerId = await target.Resolve(resolver, _fluxerRef, Ct);

        fluxerId.ShouldNotBe(discordId);
        (await target.ReadMappedIdsAsync(db)).ShouldBe([discordId, fluxerId], ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public async Task TryGet_WhenRefIsUnknown_ShouldReturnNullAndCreateNoRow(string name)
    {
        var target = IdentityTarget.Named(name);
        await using var db = Fixture.CreateDbContext();

        var id = await target.TryGet(CreateResolver(db), _discordRef, Ct);

        id.ShouldBeNull();
        (await target.ReadIdsAsync(db)).ShouldBeEmpty();
        (await target.ReadMappedIdsAsync(db)).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public async Task TryGet_WhenRefIsKnown_ShouldReturnResolvedId(string name)
    {
        var target = IdentityTarget.Named(name);
        await using var db = Fixture.CreateDbContext();
        var resolved = await target.Resolve(CreateResolver(db), _discordRef, Ct);

        var id = await target.TryGet(CreateResolver(db), _discordRef, Ct);

        id.ShouldBe(resolved);
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public async Task TryGet_WhenRefWasResolvedWithSameCache_ShouldServeIdFromCache(string name)
    {
        var target = IdentityTarget.Named(name);
        var cache = new PlatformIdCache();
        await using var db = Fixture.CreateDbContext();
        var resolved = await target.Resolve(CreateResolver(db, cache), _discordRef, Ct);
        await target.DeleteMappingsAsync(db);

        var id = await target.TryGet(CreateResolver(db, cache), _discordRef, Ct);

        id.ShouldBe(resolved);
    }

    [Theory]
    [InlineData(PlatformKind.Discord, "discord")]
    [InlineData(PlatformKind.Fluxer, "fluxer")]
    public async Task ResolveUserAsync_ShouldStorePlatformAsLowerCaseText(PlatformKind platform, string expected)
    {
        await using var db = Fixture.CreateDbContext();

        await CreateResolver(db).ResolveUserAsync(new PlatformRef(platform, "42"), Ct);

        (await db.Database.SqlQuery<string>($"""SELECT platform AS "Value" FROM user_platform_ids""").ToListAsync(Ct))
            .ShouldBe([expected]);
    }

    [Fact]
    public async Task ResolveChannelAsync_WhenChannelIsUnknown_ShouldScopeItToInternalGuild()
    {
        await using var db = Fixture.CreateDbContext();
        var resolver = CreateResolver(db);

        var channelId = await resolver.ResolveChannelAsync(IdentityTarget.GuildOf(_discordRef), _discordRef, Ct);

        var guildId = await resolver.TryGetGuildAsync(IdentityTarget.GuildOf(_discordRef), Ct);
        (await db.Channels.SingleAsync(channel => channel.Id == channelId, Ct)).GuildId.ShouldBe(guildId!.Value);
    }

    [Fact]
    public async Task ResolveRoleAsync_WhenRoleIsUnknown_ShouldScopeItToInternalGuild()
    {
        await using var db = Fixture.CreateDbContext();
        var resolver = CreateResolver(db);

        var roleId = await resolver.ResolveRoleAsync(IdentityTarget.GuildOf(_discordRef), _discordRef, Ct);

        var guildId = await resolver.TryGetGuildAsync(IdentityTarget.GuildOf(_discordRef), Ct);
        (await db.Roles.SingleAsync(role => role.Id == roleId, Ct)).GuildId.ShouldBe(guildId!.Value);
    }

    [Fact]
    public async Task ResolveChannelAsync_WhenGuildIsOnAnotherPlatform_ShouldThrow()
    {
        await using var db = Fixture.CreateDbContext();

        await Should.ThrowAsync<ArgumentException>(
            () => CreateResolver(db).ResolveChannelAsync(IdentityTarget.GuildOf(_fluxerRef), _discordRef, Ct));
    }

    [Fact]
    public async Task ResolveRoleAsync_WhenGuildIsOnAnotherPlatform_ShouldThrow()
    {
        await using var db = Fixture.CreateDbContext();

        await Should.ThrowAsync<ArgumentException>(
            () => CreateResolver(db).ResolveRoleAsync(IdentityTarget.GuildOf(_fluxerRef), _discordRef, Ct));
    }

    private static PlatformIdResolver CreateResolver(ApolloDbContext db, PlatformIdCache? cache = null) =>
        new(db, cache ?? new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
}
