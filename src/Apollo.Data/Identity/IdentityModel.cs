using Apollo.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Identity;

/// <summary>
/// The internal identity tables (<c>guilds</c>, <c>users</c>, <c>channels</c>, <c>roles</c>) and one platform ID
/// mapping table per entity (scope §4.2, §5.1.2 "No ID clashes").
/// </summary>
internal static class IdentityModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Guild>(entity =>
        {
            entity.ToTable("guilds");
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        });

        modelBuilder.Entity<Channel>(entity =>
        {
            entity.ToTable("channels");
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureMapping<GuildPlatformId, Guild>(modelBuilder, "guild_platform_ids", nameof(GuildPlatformId.GuildId), "guild_id");
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
