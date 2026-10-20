using Apollo.Data.Identity;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Names;

/// <summary>
/// The <c>member_name_changes</c> hypertable (#114).
/// </summary>
internal static class NameModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MemberNameChange>(entity =>
        {
            entity.ToTable("member_name_changes");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Nickname).HasColumnName("nickname");
            entity.Property(x => x.GlobalName).HasColumnName("global_name");
            entity.Property(x => x.Username).HasColumnName("username");
            entity.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>();

            // Hypertable rule: the primary key must contain the partition column (ADR 0001).
            entity.HasKey(x => new { x.Id, x.OccurredAt });
            entity.IsHypertable(x => x.OccurredAt);

            // Finds a member's latest row (the recorder's comparison) and their name history.
            entity.HasIndex(x => new { x.GuildId, x.UserId, x.OccurredAt });

            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
