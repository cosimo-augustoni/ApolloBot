using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Stats.Raw;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.Stats.Tests.Raw;

/// <summary>
/// A guild with Alice, Bob, Carol and a bot member, a text channel and two voice channels. Every test seeds its own
/// guild (a new internal ID after the reset). The default range is 10 to 14 January 2026 (CET, UTC+1).
/// </summary>
internal sealed record RawStatsSeed(Guid GuildId, Guid Alice, Guid Bob, Guid Carol, Guid Bot, Guid General, Guid Lounge, Guid Lobby)
{
    public static readonly DateOnly Jan10 = new(2026, 1, 10);
    public static readonly DateOnly Jan14 = new(2026, 1, 14);

    public RawStatsQuery Query(Guid? userId = null, int top = RawStatsQuery.DefaultTop, bool includeBots = false) =>
        new(GuildId, userId, Jan10, Jan14, top, includeBots);

    public VoiceSessionBuilder Voice(Guid userId, Guid channelId, string fromUtc, string? untilUtc)
    {
        var builder = new VoiceSessionBuilder().In(GuildId, channelId).By(userId).From(fromUtc);
        return untilUtc is null ? builder : builder.Until(untilUtc);
    }

    public static async Task<RawStatsSeed> CreateAsync(ApolloDbContext db, CancellationToken cancellationToken)
    {
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var guild = await new GuildBuilder().SeedAsync(db, ids, cancellationToken);
        var alice = await new GuildMemberBuilder().For(TestPlatform.User("alice")).SeedAsync(db, ids, cancellationToken);
        var bob = await new GuildMemberBuilder().For(TestPlatform.User("bob")).SeedAsync(db, ids, cancellationToken);
        var carol = await new GuildMemberBuilder().For(TestPlatform.User("carol")).SeedAsync(db, ids, cancellationToken);
        var bot = await new GuildMemberBuilder().For(TestPlatform.User("some-bot")).AsBot().SeedAsync(db, ids, cancellationToken);
        var general = await new ChannelBuilder().For(TestPlatform.Channel("general")).Named("general").SeedAsync(db, ids, cancellationToken);
        var lounge = await new ChannelBuilder().For(TestPlatform.Channel("lounge")).Named("lounge").SeedAsync(db, ids, cancellationToken);
        var lobby = await new ChannelBuilder().For(TestPlatform.Channel("lobby")).Named("lobby").SeedAsync(db, ids, cancellationToken);
        return new RawStatsSeed(guild.Id, alice.UserId, bob.UserId, carol.UserId, bot.UserId, general.Id, lounge.Id, lobby.Id);
    }

    public static async Task AddAsync(ApolloDbContext db, CancellationToken cancellationToken, params object[] rows)
    {
        db.AddRange(rows);
        await db.SaveChangesAsync(cancellationToken);
    }
}
