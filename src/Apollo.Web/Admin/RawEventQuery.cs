using Apollo.Data;
using Apollo.Domain.Messages;
using Apollo.Platform.Abstractions.Identity;
using Apollo.Web.Client.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Web.Admin;

/// <summary>
/// The raw event browser of the admin data page (#32, scope §7.2): one page of one raw event table of the configured
/// guild (scope §4.3), newest first (<c>occurred_at DESC, id DESC</c>, so pages are stable), with the total. Only
/// metadata columns are read; message content is never stored (scope §2). References to untracked users are
/// <see langword="null"/> in the tables (scope §5.1.1) and stay <see langword="null"/>.
/// </summary>
public sealed class RawEventQuery(ApolloDbContext db, IEnumerable<IPlatformIdentityProvider> providers)
{
    public const int DefaultPageSize = 50;

    public const int MaxPageSize = 200;

    /// <summary>The time range browsed when the caller names none, ending now.</summary>
    public static readonly TimeSpan DefaultRange = TimeSpan.FromHours(24);

    /// <summary>The longest range a caller may ask for, so every query stays bounded in time (chunk exclusion).</summary>
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(366);

    /// <summary>The most choices <see cref="SearchUsersAsync"/> and <see cref="SearchChannelsAsync"/> return.</summary>
    public const int MaxSearchResults = 20;

    public async Task<RawEventPageDto> GetPageAsync(RawEventSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);
        var guilds = await ConfiguredGuilds.IdsAsync(db, providers, cancellationToken);

        return search.Type switch
        {
            DataEventType.Message => await PageAsync(
                db.MessageEvents.Select(e => new Row<Data.Messages.MessageEvent>
                {
                    Id = e.Id,
                    GuildId = e.GuildId,
                    OccurredAt = e.OccurredAt,
                    UserId = e.UserId,
                    ChannelId = e.ChannelId,
                    Event = e,
                }),
                search,
                guilds,
                e => [e.ReplyToUserId],
                e => [e.ThreadParentId],
                (row, e, names) => row with
                {
                    Message = new MessageEventDetailsDto(
                        e.Kind.ToString(),
                        e.Length,
                        e.WordCount,
                        e.AttachmentCount,
                        e.EmbedCount,
                        e.EmojiCount,
                        e.UserMentionCount,
                        e.RoleMentionCount,
                        e.MentionsEveryone,
                        e.ContentParts is { } parts ? PartNames(parts) : null,
                        names.Channel(e.ThreadParentId),
                        names.User(e.ReplyToUserId),
                        e.IsBot),
                },
                cancellationToken),
            DataEventType.VoiceSession => await PageAsync(
                db.VoiceSessions.Select(e => new Row<Data.Voice.VoiceSession>
                {
                    Id = e.Id,
                    GuildId = e.GuildId,
                    OccurredAt = e.OccurredAt,
                    UserId = e.UserId,
                    ChannelId = e.ChannelId,
                    Event = e,
                }),
                search,
                guilds,
                _ => [],
                _ => [],
                (row, e, _) => row with { VoiceSession = new VoiceSessionDetailsDto(e.EndedAt, e.EndReason?.ToString()) },
                cancellationToken),
            DataEventType.VoiceStateSegment => await PageAsync(
                db.VoiceStateSegments.Select(e => new Row<Data.Voice.VoiceStateSegment>
                {
                    Id = e.Id,
                    GuildId = e.GuildId,
                    OccurredAt = e.OccurredAt,
                    UserId = e.UserId,
                    ChannelId = null,
                    Event = e,
                }),
                search,
                guilds,
                _ => [],
                _ => [],
                (row, e, _) => row with
                {
                    VoiceSegment = new VoiceSegmentDetailsDto(e.SessionId, e.EndedAt, e.SelfMute, e.SelfDeaf, e.Streaming, e.Video),
                },
                cancellationToken),
            DataEventType.Reaction => await PageAsync(
                db.ReactionEvents.Select(e => new Row<Data.Reactions.ReactionEvent>
                {
                    Id = e.Id,
                    GuildId = e.GuildId,
                    OccurredAt = e.OccurredAt,
                    UserId = e.UserId,
                    ChannelId = e.ChannelId,
                    Event = e,
                }),
                search,
                guilds,
                e => [e.MessageAuthorId],
                _ => [],
                (row, e, names) => row with
                {
                    Reaction = new ReactionEventDetailsDto(e.Kind.ToString(), e.EmojiName, e.EmojiCustomId is not null, names.User(e.MessageAuthorId)),
                },
                cancellationToken),
            DataEventType.Membership => await PageAsync(
                db.MembershipEvents.Select(e => new Row<Data.Membership.MembershipEvent>
                {
                    Id = e.Id,
                    GuildId = e.GuildId,
                    OccurredAt = e.OccurredAt,
                    UserId = e.UserId,
                    ChannelId = null,
                    Event = e,
                }),
                search,
                guilds,
                _ => [],
                _ => [],
                (row, e, _) => row with { Membership = new MembershipEventDetailsDto(e.Kind.ToString()) },
                cancellationToken),
            DataEventType.PresenceActivity => await PageAsync(
                db.PresenceActivitySessions.Select(e => new Row<Data.Presence.PresenceActivitySession>
                {
                    Id = e.Id,
                    GuildId = e.GuildId,
                    OccurredAt = e.OccurredAt,
                    UserId = e.UserId,
                    ChannelId = null,
                    Event = e,
                }),
                search,
                guilds,
                _ => [],
                _ => [],
                (row, e, _) => row with { PresenceActivity = new PresenceActivityDetailsDto(e.ActivityType.ToString(), e.ActivityName, e.EndedAt) },
                cancellationToken),
            DataEventType.PresenceActivitySegment => await PageAsync(
                db.PresenceActivitySegments.Select(e => new Row<Data.Presence.PresenceActivitySegment>
                {
                    Id = e.Id,
                    GuildId = e.GuildId,
                    OccurredAt = e.OccurredAt,
                    UserId = e.UserId,
                    ChannelId = null,
                    Event = e,
                }),
                search,
                guilds,
                _ => [],
                _ => [],
                (row, e, _) => row with
                {
                    PresenceSegment = new PresenceSegmentDetailsDto(
                        e.ActivityType.ToString(),
                        e.ActivityName,
                        e.EndedAt,
                        e.Details,
                        e.State,
                        e.LargeText,
                        e.SmallText,
                        e.PartyExtId,
                        e.PartySize,
                        e.PartyMax,
                        e.ApplicationExtId,
                        e.ReportedStart,
                        e.ReportedEnd),
                },
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(search), search.Type, "Unknown event type."),
        };
    }

    /// <summary>
    /// Up to <see cref="MaxSearchResults"/> members of the configured guild, departed ones included (their events
    /// stay), whose display name contains <paramref name="search"/> (any case), by name: the user filter's choices.
    /// </summary>
    public async Task<IReadOnlyList<EventUserDto>> SearchUsersAsync(string? search, CancellationToken cancellationToken)
    {
        var guilds = await ConfiguredGuilds.IdsAsync(db, providers, cancellationToken);
        var term = (search ?? string.Empty).Trim().ToLowerInvariant();
        return await db.Users.AsNoTracking()
            .Where(u => u.DisplayName != null
                && u.DisplayName.ToLower().Contains(term)
                && db.GuildMembers.Any(m => m.UserId == u.Id && guilds.Contains(m.GuildId)))
            .OrderBy(u => u.DisplayName)
            .Take(MaxSearchResults)
            .Select(u => new EventUserDto(u.Id, u.DisplayName))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Up to <see cref="MaxSearchResults"/> channels and threads of the configured guild, deleted ones included, whose
    /// name contains <paramref name="search"/> (any case), by name: the channel filter's choices.
    /// </summary>
    public async Task<IReadOnlyList<EventChannelDto>> SearchChannelsAsync(string? search, CancellationToken cancellationToken)
    {
        var guilds = await ConfiguredGuilds.IdsAsync(db, providers, cancellationToken);
        var term = (search ?? string.Empty).Trim().ToLowerInvariant();
        return await db.Channels.AsNoTracking()
            .Where(c => c.Name != null && c.Name.ToLower().Contains(term) && guilds.Contains(c.GuildId))
            .OrderBy(c => c.Name)
            .Take(MaxSearchResults)
            .Select(c => new EventChannelDto(c.Id, c.Name))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Filters, counts and pages <paramref name="rows"/>, then names the users and channels of the page in two queries.
    /// <paramref name="referencedUsers"/> and <paramref name="referencedChannels"/> name the type's other references.
    /// </summary>
    private async Task<RawEventPageDto> PageAsync<TEvent>(
        IQueryable<Row<TEvent>> rows,
        RawEventSearch search,
        Guid[] guilds,
        Func<TEvent, Guid?[]> referencedUsers,
        Func<TEvent, Guid?[]> referencedChannels,
        Func<RawEventDto, TEvent, Names, RawEventDto> withDetails,
        CancellationToken cancellationToken)
    {
        rows = rows.AsNoTracking().Where(r => guilds.Contains(r.GuildId) && r.OccurredAt >= search.From && r.OccurredAt < search.To);
        if (search.UserId is { } userId)
        {
            rows = rows.Where(r => r.UserId == userId);
        }

        if (search.ChannelId is { } channelId)
        {
            // Tables without a channel project NULL, so no row matches: the filters combine with AND.
            rows = rows.Where(r => r.ChannelId == channelId);
        }

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .OrderByDescending(r => r.OccurredAt)
            .ThenByDescending(r => r.Id)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = page.Select(r => r.UserId).Concat(page.SelectMany(r => referencedUsers(r.Event)).OfType<Guid>()).Distinct().ToList();
        var channelIds = page.Select(r => r.ChannelId).Concat(page.SelectMany(r => referencedChannels(r.Event))).OfType<Guid>().Distinct().ToList();
        var names = new Names(
            await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken),
            await db.Channels.AsNoTracking().Where(c => channelIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken));

        return new RawEventPageDto(total, [.. page.Select(r => withDetails(
            new RawEventDto(r.Id, search.Type, r.OccurredAt, names.User(r.UserId), names.Channel(r.ChannelId)),
            r.Event,
            names))]);
    }

    private static string[] PartNames(MessageContentParts parts) =>
        [.. Enum.GetValues<MessageContentParts>().Where(p => p != MessageContentParts.None && parts.HasFlag(p)).Select(p => p.ToString())];

    /// <summary>The common columns of every raw event table, so one filter and order serve all of them.</summary>
    private sealed class Row<TEvent>
    {
        public long Id { get; init; }

        public Guid GuildId { get; init; }

        public DateTimeOffset OccurredAt { get; init; }

        public Guid UserId { get; init; }

        public Guid? ChannelId { get; init; }

        public required TEvent Event { get; init; }
    }

    private sealed class Names(Dictionary<Guid, string?> users, Dictionary<Guid, string?> channels)
    {
        public EventUserDto User(Guid id) => new(id, users.GetValueOrDefault(id));

        public EventUserDto? User(Guid? id) => id is { } value ? User(value) : null;

        public EventChannelDto? Channel(Guid? id) => id is { } value ? new EventChannelDto(value, channels.GetValueOrDefault(value)) : null;
    }
}
