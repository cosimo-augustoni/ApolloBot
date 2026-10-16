using Apollo.Data.Identity;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Guilds;

/// <summary>
/// <c>guild_members</c> and <c>member_roles</c>, the guild state the sync (#13) mirrors. Reference tables, not event
/// tables: rows are updated and replaced.
/// </summary>
internal static class GuildStateModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GuildMember>(entity =>
        {
            entity.ToTable("guild_members");
            entity.HasKey(x => new { x.GuildId, x.UserId });
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.JoinedAt).HasColumnName("joined_at");
            entity.Property(x => x.IsAdministrator).HasColumnName("is_administrator");
            entity.Property(x => x.IsBot).HasColumnName("is_bot");
            entity.Property(x => x.LeftAt).HasColumnName("left_at");
            entity.HasIndex(x => x.UserId);
            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MemberRole>(entity =>
        {
            entity.ToTable("member_roles");
            entity.HasKey(x => new { x.GuildId, x.UserId, x.RoleId });
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.RoleId).HasColumnName("role_id");
            entity.HasIndex(x => x.RoleId);
            entity.HasOne<GuildMember>().WithMany().HasForeignKey(x => new { x.GuildId, x.UserId }).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
