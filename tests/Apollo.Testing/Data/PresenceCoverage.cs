using Apollo.Data;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Testing.Data;

/// <summary>
/// Checks the #115 rule that every presence session is covered by its segments (same guild, user, type and name)
/// from start to end, without gaps or overlaps.
/// </summary>
public static class PresenceCoverage
{
    /// <summary>
    /// The sessions whose segments do not cover them exactly, with the reason; empty if every session is covered.
    /// </summary>
    public static async Task<IReadOnlyList<string>> FindUncoveredAsync(ApolloDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var sessions = await db.PresenceActivitySessions.AsNoTracking().ToListAsync(cancellationToken);
        var segments = await db.PresenceActivitySegments.AsNoTracking().ToListAsync(cancellationToken);
        var problems = new List<string>();
        foreach (var session in sessions)
        {
            var own = segments
                .Where(s => s.GuildId == session.GuildId && s.UserId == session.UserId && s.ActivityType == session.ActivityType
                    && s.ActivityName == session.ActivityName && s.OccurredAt >= session.OccurredAt && s.EndedAt <= session.EndedAt)
                .OrderBy(s => s.OccurredAt)
                .ToList();
            if (own.Count == 0)
            {
                problems.Add($"session {session.Id} has no segments");
                continue;
            }

            if (own[0].OccurredAt != session.OccurredAt || own[^1].EndedAt != session.EndedAt)
            {
                problems.Add($"session {session.Id} is not covered from start to end");
            }

            problems.AddRange(own.Zip(own.Skip(1))
                .Where(pair => pair.First.EndedAt != pair.Second.OccurredAt)
                .Select(pair => $"session {session.Id} has a gap or overlap at {pair.First.EndedAt:O}"));
        }

        problems.AddRange(segments
            .Where(s => !sessions.Any(session => s.GuildId == session.GuildId && s.UserId == session.UserId
                && s.ActivityType == session.ActivityType && s.ActivityName == session.ActivityName
                && s.OccurredAt >= session.OccurredAt && s.EndedAt <= session.EndedAt))
            .Select(s => $"segment {s.Id} belongs to no session"));
        return problems;
    }
}
