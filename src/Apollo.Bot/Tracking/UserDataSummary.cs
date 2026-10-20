using Apollo.Data;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Bot.Tracking;

/// <inheritdoc cref="IUserDataSummary"/>
/// <remarks>
/// A recorder issue that adds an event table adds it here too, and to <c>RecordedDataEraserStep</c> (privacy delete).
/// The tables are hypertables on <c>occurred_at</c>, so each count looks at every chunk; fine for one user on demand.
/// </remarks>
internal sealed class UserDataSummary(ApolloDbContext db) : IUserDataSummary
{
    public async Task<UserDataCounts> CountAsync(Guid guildId, Guid userId, CancellationToken cancellationToken) =>
        new(
            await db.MessageEvents.LongCountAsync(e => e.GuildId == guildId && e.UserId == userId, cancellationToken),
            await db.ReactionEvents.LongCountAsync(e => e.GuildId == guildId && e.UserId == userId, cancellationToken),
            await db.VoiceSessions.LongCountAsync(e => e.GuildId == guildId && e.UserId == userId, cancellationToken),
            await db.PresenceActivitySessions.LongCountAsync(e => e.GuildId == guildId && e.UserId == userId, cancellationToken),
            await db.MembershipEvents.LongCountAsync(e => e.GuildId == guildId && e.UserId == userId, cancellationToken),
            await db.MemberNameChanges.LongCountAsync(e => e.GuildId == guildId && e.UserId == userId, cancellationToken));
}
