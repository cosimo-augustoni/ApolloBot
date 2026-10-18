using Apollo.Data;
using Apollo.Platform.Abstractions.Identity;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Web.Admin;

/// <summary>
/// The guilds the admin data page (#32) shows: the configured guild of each identity provider (scope §4.3).
/// <c>BotStatusQuery</c> still has its own copy; #91 may switch it to this one.
/// </summary>
public static class ConfiguredGuilds
{
    /// <summary>The internal IDs of the configured guilds the bot has seen; empty before it saw any.</summary>
    public static async Task<Guid[]> IdsAsync(ApolloDbContext db, IEnumerable<IPlatformIdentityProvider> providers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(providers);

        var guildIds = new List<Guid>();
        foreach (var guild in providers.Select(p => p.Guild).Distinct())
        {
            guildIds.AddRange(await db.GuildPlatformIds.AsNoTracking()
                .Where(m => m.Platform == guild.Platform && m.ExternalId == guild.ExternalId)
                .Select(m => m.GuildId)
                .ToListAsync(cancellationToken));
        }

        return [.. guildIds.Distinct()];
    }
}
