using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Membership;
using Apollo.Data.Messages;
using Apollo.Data.Presence;
using Apollo.Data.Reactions;
using Apollo.Data.Voice;
using Apollo.Domain.Messages;
using Apollo.Domain.Presence;
using Apollo.Platform.Abstractions;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Apollo.Web.Client.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.Web.Tests.Admin;

/// <summary>
/// Seeds rows of the raw event tables (#32) as the recorders store them, by default by Alice in #general of the
/// configured guild (<see cref="ApolloWebApplicationFactory.Guild"/>).
/// </summary>
internal sealed class EventSeeder(TimescaleFixture fixture, CancellationToken cancellationToken)
{
    public static readonly PlatformRef Alice = TestPlatform.User("alice");
    public static readonly PlatformRef General = TestPlatform.Channel("general");

    /// <summary>The internal ID of <paramref name="user"/>, named <paramref name="displayName"/>.</summary>
    public async Task<Guid> UserAsync(PlatformRef user, string displayName)
    {
        await using var db = fixture.CreateDbContext();
        return (await new UserBuilder().For(user).Named(displayName).SeedAsync(db, Ids(db), cancellationToken)).Id;
    }

    /// <summary>The internal ID of <paramref name="channel"/> in the configured guild, named <paramref name="name"/>.</summary>
    public async Task<Guid> ChannelAsync(PlatformRef channel, string name)
    {
        await using var db = fixture.CreateDbContext();
        return (await new ChannelBuilder().For(channel, ApolloWebApplicationFactory.Guild).Named(name).SeedAsync(db, Ids(db), cancellationToken)).Id;
    }

    /// <summary>One row of <paramref name="type"/>'s table; returns its ID.</summary>
    public async Task<long> AddAsync(
        DataEventType type,
        DateTimeOffset at,
        Guid? userId = null,
        Guid? channelId = null,
        PlatformRef? guild = null,
        Guid? referencedUserId = null)
    {
        await using var db = fixture.CreateDbContext();
        var ids = Ids(db);
        var guildRef = guild ?? ApolloWebApplicationFactory.Guild;
        var guildId = await ids.ResolveGuildAsync(guildRef, cancellationToken);
        var user = userId ?? await ids.ResolveUserAsync(Alice, cancellationToken);
        var channel = channelId ?? await ids.ResolveChannelAsync(guildRef, General, cancellationToken);

        switch (type)
        {
            case DataEventType.Message:
                var message = new MessageEvent
                {
                    GuildId = guildId,
                    UserId = user,
                    ChannelId = channel,
                    OccurredAt = at,
                    MessageExtId = "700000000000000001",
                    Kind = MessageEventKind.Create,
                    Length = 42,
                    WordCount = 7,
                    AttachmentCount = 1,
                    EmbedCount = 0,
                    EmojiCount = 0,
                    UserMentionCount = 0,
                    RoleMentionCount = 0,
                    MentionsEveryone = false,
                    ContentParts = MessageContentParts.Text | MessageContentParts.Attachment,
                    ReplyToUserId = referencedUserId,
                };
                db.MessageEvents.Add(message);
                await db.SaveChangesAsync(cancellationToken);
                return message.Id;
            case DataEventType.VoiceSession:
                var session = new VoiceSession { GuildId = guildId, UserId = user, ChannelId = channel, OccurredAt = at };
                db.VoiceSessions.Add(session);
                await db.SaveChangesAsync(cancellationToken);
                return session.Id;
            case DataEventType.VoiceStateSegment:
                var segment = new VoiceStateSegment { GuildId = guildId, UserId = user, SessionId = 1, OccurredAt = at, SelfMute = true };
                db.VoiceStateSegments.Add(segment);
                await db.SaveChangesAsync(cancellationToken);
                return segment.Id;
            case DataEventType.Reaction:
                var reaction = new ReactionEvent
                {
                    GuildId = guildId,
                    UserId = user,
                    ChannelId = channel,
                    OccurredAt = at,
                    MessageExtId = "700000000000000001",
                    MessageAuthorId = referencedUserId,
                    EmojiName = "👍",
                    Kind = ReactionEventKind.Add,
                };
                db.ReactionEvents.Add(reaction);
                await db.SaveChangesAsync(cancellationToken);
                return reaction.Id;
            case DataEventType.Membership:
                var membership = new MembershipEvent { GuildId = guildId, UserId = user, OccurredAt = at, Kind = MembershipEventKind.Join };
                db.MembershipEvents.Add(membership);
                await db.SaveChangesAsync(cancellationToken);
                return membership.Id;
            case DataEventType.PresenceActivity:
                var presence = new PresenceActivitySession
                {
                    GuildId = guildId,
                    UserId = user,
                    OccurredAt = at,
                    EndedAt = at.AddMinutes(5),
                    ActivityType = PresenceActivityType.Playing,
                    ActivityName = "Factorio",
                };
                db.PresenceActivitySessions.Add(presence);
                await db.SaveChangesAsync(cancellationToken);
                return presence.Id;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private static PlatformIdResolver Ids(ApolloDbContext db) => new(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
}
