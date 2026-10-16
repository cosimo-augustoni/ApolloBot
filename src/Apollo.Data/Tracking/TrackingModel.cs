using Apollo.Data.Identity;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Tracking;

/// <summary>
/// <c>role_settings</c>, its audit log <c>role_setting_changes</c> and <c>tracking_periods</c> (#14). None of them is an
/// event table, so they are plain tables, not hypertables.
/// </summary>
internal static class TrackingModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoleSetting>(entity =>
        {
            entity.ToTable("role_settings");
            entity.HasKey(x => new { x.GuildId, x.Platform, x.Purpose, x.RoleId });
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.Platform).HasColumnName("platform").HasConversion(new PlatformKindConverter());
            entity.Property(x => x.RoleId).HasColumnName("role_id");
            entity.Property(x => x.Purpose).HasColumnName("purpose").HasConversion<string>();
            entity.Property(x => x.ChangedByUserId).HasColumnName("changed_by_user_id");
            entity.Property(x => x.ChangedAt).HasColumnName("changed_at");
            entity.HasIndex(x => x.RoleId);

            // At most one tracking role per (guild, platform), scope §5.1.2.
            entity.HasIndex(x => new { x.GuildId, x.Platform }, "ux_role_settings_one_tracking_role")
                .IsUnique()
                .HasFilter("purpose = 'Tracking'");

            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoleSettingChange>(entity =>
        {
            entity.ToTable("role_setting_changes");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.Platform).HasColumnName("platform").HasConversion(new PlatformKindConverter());
            entity.Property(x => x.Purpose).HasColumnName("purpose").HasConversion<string>();
            entity.Property(x => x.RoleId).HasColumnName("role_id");
            entity.Property(x => x.Action).HasColumnName("action").HasConversion<string>();
            entity.Property(x => x.ChangedByUserId).HasColumnName("changed_by_user_id");
            entity.Property(x => x.ChangedAt).HasColumnName("changed_at");
            entity.HasIndex(x => new { x.GuildId, x.Platform, x.Purpose });
            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TrackingPeriod>(entity =>
        {
            entity.ToTable("tracking_periods");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.From).HasColumnName("from");
            entity.Property(x => x.To).HasColumnName("to");
            // At most one open period per member.
            entity.HasIndex(x => new { x.GuildId, x.UserId }, "ux_tracking_periods_one_open_period")
                .IsUnique()
                .HasFilter("\"to\" IS NULL");
            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
