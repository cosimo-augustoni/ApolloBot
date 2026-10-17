using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Sessions;

/// <summary>Reads and writes <c>user_sign_outs</c>, the last website logout per user (#30).</summary>
public sealed class UserSignOuts(ApolloDbContext db)
{
    /// <summary>
    /// Stores <paramref name="signedOutAt"/> as the user's last logout, unless a later one is stored already.
    /// </summary>
    public async Task RecordAsync(Guid userId, DateTimeOffset signedOutAt, CancellationToken cancellationToken)
    {
        // EF Core has no upsert; ON CONFLICT keeps two logouts at the same moment (two devices) from failing on the key.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO user_sign_outs (user_id, signed_out_at) VALUES ({userId}, {signedOutAt})
            ON CONFLICT (user_id) DO UPDATE SET signed_out_at = GREATEST(user_sign_outs.signed_out_at, EXCLUDED.signed_out_at)
            """,
            cancellationToken);
    }

    /// <summary>The user's last logout, or <see langword="null"/> when they never logged out.</summary>
    public async Task<DateTimeOffset?> GetLastAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserSignOuts
            .Where(s => s.UserId == userId)
            .Select(s => (DateTimeOffset?)s.SignedOutAt)
            .SingleOrDefaultAsync(cancellationToken);
}
