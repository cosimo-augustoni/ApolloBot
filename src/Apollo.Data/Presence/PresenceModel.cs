using Apollo.Data.Identity;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Presence;

/// <summary>
/// The <c>presence_activity_sessions</c> (#19) and <c>presence_activity_segments</c> (#115) hypertables.
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

        modelBuilder.Entity<PresenceActivitySegment>(entity =>
        {
            entity.ToTable("presence_activity_segments");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.ActivityType).HasColumnName("activity_type").HasConversion<string>();
            entity.Property(x => x.ActivityName).HasColumnName("activity_name");
            entity.Property(x => x.EndedAt).HasColumnName("ended_at");
            entity.Property(x => x.Details).HasColumnName("details");
            entity.Property(x => x.State).HasColumnName("state");
            entity.Property(x => x.LargeText).HasColumnName("large_text");
            entity.Property(x => x.SmallText).HasColumnName("small_text");
            entity.Property(x => x.PartyExtId).HasColumnName("party_ext_id");
            entity.Property(x => x.PartySize).HasColumnName("party_size");
            entity.Property(x => x.PartyMax).HasColumnName("party_max");
            entity.Property(x => x.ApplicationExtId).HasColumnName("application_ext_id");
            entity.Property(x => x.ReportedStart).HasColumnName("reported_start");
            entity.Property(x => x.ReportedEnd).HasColumnName("reported_end");

            entity.HasKey(x => new { x.Id, x.OccurredAt });
            entity.IsHypertable(x => x.OccurredAt);

            // No foreign key to presence_activity_sessions (its primary key is (id, occurred_at)): a segment belongs to
            // the session of the same user, type and name whose span contains it, found through this index.
            entity.HasIndex(x => new { x.GuildId, x.UserId, x.OccurredAt });

            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
