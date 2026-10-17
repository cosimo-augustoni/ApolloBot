using Apollo.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Guilds;

/// <summary>
/// Reads a user's access in a guild from the guild state the bot syncs (#13, scope §6.2): <c>guild_members</c> with
/// <c>left_at IS NULL</c> and its <c>is_administrator</c> flag, which already includes the guild owner. The web host
/// never asks the bot.
/// </summary>
public sealed class GuildAccessReader(ApolloDbContext db)
{
    /// <summary>The access of the internal user <paramref name="userId"/> in the guild <paramref name="guild"/>.</summary>
    public async Task<GuildAccess> GetAsync(Guid userId, PlatformRef guild, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(guild);

        var isAdministrator = await (
                from mapping in db.GuildPlatformIds
                where mapping.Platform == guild.Platform && mapping.ExternalId == guild.ExternalId
                join member in db.GuildMembers on mapping.GuildId equals member.GuildId
                where member.UserId == userId && member.LeftAt == null
                select (bool?)member.IsAdministrator)
            .SingleOrDefaultAsync(cancellationToken);

        return isAdministrator switch
        {
            null => GuildAccess.NotMember,
            true => GuildAccess.Administrator,
            false => GuildAccess.Member,
        };
    }
}
