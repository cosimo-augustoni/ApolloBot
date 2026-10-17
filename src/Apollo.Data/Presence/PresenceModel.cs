using Apollo.Data.Identity;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Presence;

/// <summary>
/// The <c>presence_activity_sessions</c> hypertable (#19).
/// </summary>
internal static class PresenceModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PresenceActivitySession>(entity =>
        {
            entity.ToTable("presence_activity_sessions");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.ActivityType).HasColumnName("activity_type").HasConversion<string>();
            entity.Property(x => x.ActivityName).HasColumnName("activity_name");
            entity.Property(x => x.EndedAt).HasColumnName("ended_at");

            // Hypertable rule: the primary key must contain the partition column (ADR 0001).
            entity.HasKey(x => new { x.Id, x.OccurredAt });
            entity.IsHypertable(x => x.OccurredAt);

            // A user's history (their top games, hours played) and per-user deletes.
            entity.HasIndex(x => new { x.GuildId, x.UserId, x.OccurredAt });

            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
