using Apollo.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Identity;

/// <summary>
/// The internal identity tables (<c>guilds</c>, <c>users</c>, <c>channels</c>, <c>roles</c>) with their snapshot
/// columns (#13), and one platform ID mapping table per entity (scope §4.2, §5.1.2 "No ID clashes").
/// </summary>
internal static class IdentityModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Guild>(entity =>
        {
            entity.ToTable("guilds");
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.OwnerUserId).HasColumnName("owner_user_id");
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.DisplayName).HasColumnName("display_name");
            entity.Property(x => x.AvatarUrl).HasColumnName("avatar_url");
        });

        modelBuilder.Entity<Channel>(entity =>
        {
            entity.ToTable("channels");
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.Type).HasColumnName("type").HasConversion<string>();
            entity.Property(x => x.CategoryId).HasColumnName("category_id");
            entity.Property(x => x.ThreadParentId).HasColumnName("thread_parent_id");
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Channel>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Channel>().WithMany().HasForeignKey(x => x.ThreadParentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.Color).HasColumnName("color");
            entity.Property(x => x.Position).HasColumnName("position");
            entity.Property(x => x.IsAdministrator).HasColumnName("is_administrator");
            entity.Property(x => x.ManagesRoles).HasColumnName("manages_roles");
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureMapping<GuildPlatformId, Guild>(modelBuilder, "guild_platform_ids", nameof(GuildPlatformId.GuildId), "guild_id");
        modelBuilder.Entity<GuildPlatformId>(entity =>
        {
            entity.Property(x => x.BotUserId).HasColumnName("bot_user_id");
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.BotUserId).OnDelete(DeleteBehavior.Restrict);
        });
        ConfigureMapping<UserPlatformId, User>(modelBuilder, "user_platform_ids", nameof(UserPlatformId.UserId), "user_id");
        ConfigureMapping<ChannelPlatformId, Channel>(modelBuilder, "channel_platform_ids", nameof(ChannelPlatformId.ChannelId), "channel_id");
        ConfigureMapping<RolePlatformId, Role>(modelBuilder, "role_platform_ids", nameof(RolePlatformId.RoleId), "role_id");
    }

    /// <summary>
    /// The primary key <c>(platform, external_id)</c> is the unique index the resolver's
    /// <c>ON CONFLICT</c> relies on.
    /// </summary>
    private static void ConfigureMapping<TMapping, TOwner>(
        ModelBuilder modelBuilder,
        string table,
        string ownerIdProperty,
        string ownerIdColumn)
        where TMapping : class
        where TOwner : class
    {
        modelBuilder.Entity<TMapping>(entity =>
        {
            entity.ToTable(table);
            entity.Property<PlatformKind>("Platform")
                .HasColumnName("platform")
                .HasConversion(new PlatformKindConverter());
            entity.Property<string>("ExternalId").HasColumnName("external_id");
            entity.Property<Guid>(ownerIdProperty).HasColumnName(ownerIdColumn);
            entity.HasKey("Platform", "ExternalId");
            entity.HasIndex(ownerIdProperty);
            entity.HasOne<TOwner>().WithMany().HasForeignKey(ownerIdProperty).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
