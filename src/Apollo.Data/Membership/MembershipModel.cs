using Apollo.Data.Identity;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Membership;

/// <summary>
/// The <c>membership_events</c> hypertable (#18).
/// </summary>
internal static class MembershipModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MembershipEvent>(entity =>
        {
            entity.ToTable("membership_events");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>();

            // Hypertable rule: the primary key must contain the partition column (ADR 0001).
            entity.HasKey(x => new { x.Id, x.OccurredAt });
            entity.IsHypertable(x => x.OccurredAt);

            // Finds a user's latest row of a kind (the recorder's duplicate check) and a user's history (tenure).
            entity.HasIndex(x => new { x.GuildId, x.UserId, x.Kind, x.OccurredAt });

            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
