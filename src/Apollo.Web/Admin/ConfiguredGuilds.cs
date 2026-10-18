using Apollo.Data;
using Apollo.Platform.Abstractions.Identity;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Web.Admin;

/// <summary>
/// The guilds the admin pages show (bot status #31/#91, data #32): the configured guild of each identity provider (scope §4.3).
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
