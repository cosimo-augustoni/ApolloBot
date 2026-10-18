using Apollo.Data;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Identity;
using Apollo.Stats.Activity;
using Apollo.Stats.Raw;
using Apollo.Stats.Tracking;
using Apollo.Web.Client.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Web.Admin;

/// <summary>
/// One member's stats page (#38, scope §7.2): their name and avatar, their tracking status (scope §5.1.1), their
/// activity (#35) and their raw-data stats (#36) over Zurich days, bots left out. The raw stats name other members and
/// channels by internal ID, so the names are read here.
/// </summary>
public sealed class MemberStatsQuery(
    ApolloDbContext db,
    IEnumerable<IPlatformIdentityProvider> providers,
    ITrackingStatusService tracking,
    IActivityStatsService activity,
    IRawStatsService raw)
{
    /// <summary>The page, or <see langword="null"/> when the user is no member (current or departed) of a configured guild.</summary>
    /// <exception cref="ArgumentException"><paramref name="from"/> is after <paramref name="to"/>.</exception>
    public async Task<MemberStatsDto?> GetAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var guildIds = await ConfiguredGuilds.IdsAsync(db, providers, cancellationToken);
        var member = await db.GuildMembers.AsNoTracking()
            .Where(m => m.UserId == userId && guildIds.Contains(m.GuildId))
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { m.GuildId, u.DisplayName, u.AvatarUrl })
            .FirstOrDefaultAsync(cancellationToken);
        if (member is null)
        {
            return null;
        }

        var platform = await db.GuildPlatformIds.AsNoTracking()
            .Where(g => g.GuildId == member.GuildId)
            .Select(g => (PlatformKind?)g.Platform)
            .FirstOrDefaultAsync(cancellationToken);
        var status = await tracking.GetAsync(member.GuildId, userId, cancellationToken);
        var stats = await activity.GetAsync(new ActivityStatsQuery(member.GuildId, userId, from, to), cancellationToken);

        // One context, so the raw queries run one after another.
        var query = new RawStatsQuery(member.GuildId, userId, from, to);
        var emojis = await raw.GetTopEmojisAsync(query, cancellationToken);
        var buddies = await raw.GetVoiceBuddiesAsync(query, cancellationToken);
        var replies = await raw.GetReplyGraphAsync(query, cancellationToken);
        var longest = await raw.GetLongestVoiceSessionAsync(query, cancellationToken);
        var games = await raw.GetTopGamesAsync(query, cancellationToken);

        var names = await UserNamesAsync(
            [.. buddies.Select(b => b.BuddyId), .. replies.RepliesTo.Select(e => e.RepliedToId), .. replies.RepliedBy.Select(e => e.AuthorId)],
            cancellationToken);
        EventUserDto User(Guid id) => new(id, names.GetValueOrDefault(id));

        return new MemberStatsDto(
            new MemberProfileDto(userId, member.DisplayName, member.AvatarUrl, platform?.ToString()),
            new TrackingStatusDto(status.TrackedSince, status.TrackedUntil),
            stats.ToContract(),
            new MemberHighlightsDto(
                [.. emojis.Select(e => new EmojiUsageDto(e.Name, e.CustomId is not null, e.Count))],
                [.. buddies.Select(b => new VoiceBuddyDto(User(b.BuddyId), b.OverlapSeconds))],
                [.. replies.RepliesTo.Select(e => new ReplyPartnerDto(User(e.RepliedToId), e.Count))],
                [.. replies.RepliedBy.Select(e => new ReplyPartnerDto(User(e.AuthorId), e.Count))],
                longest is null ? null : await LongestAsync(longest, cancellationToken),
                [.. games.Select(g => new GamePlaytimeDto(g.Name, g.Seconds))]));
    }

    private async Task<Dictionary<Guid, string?>> UserNamesAsync(Guid[] userIds, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

    private async Task<LongestVoiceSessionDto> LongestAsync(LongestVoiceSession session, CancellationToken cancellationToken)
    {
        var name = await db.Channels.AsNoTracking()
            .Where(c => c.Id == session.ChannelId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return new LongestVoiceSessionDto(new EventChannelDto(session.ChannelId, name), session.StartedAt, (long)session.Duration.TotalSeconds);
    }
}
