using Apollo.Data.DataProtection;
using Apollo.Data.Guilds;
using Apollo.Data.Identity;
using Apollo.Data.Messages;
using Apollo.Data.Status;
using Apollo.Data.Tracking;
using CmdScale.EntityFrameworkCore.TimescaleDB;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data;

/// <summary>
/// The one database all Apollo processes share (scope §5.2). Schema changes are migrations in this project,
/// applied by Apollo.MigrationService.
/// </summary>
public sealed class ApolloDbContext(DbContextOptions<ApolloDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    /// <summary>
    /// The Aspire resource name, and so the key under <c>ConnectionStrings</c>.
    /// </summary>
    public const string ConnectionName = "apollo";

    public DbSet<Guild> Guilds => Set<Guild>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Channel> Channels => Set<Channel>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<GuildMember> GuildMembers => Set<GuildMember>();

    public DbSet<MemberRole> MemberRoles => Set<MemberRole>();

    public DbSet<GuildPlatformId> GuildPlatformIds => Set<GuildPlatformId>();

    public DbSet<UserPlatformId> UserPlatformIds => Set<UserPlatformId>();

    public DbSet<ChannelPlatformId> ChannelPlatformIds => Set<ChannelPlatformId>();

    public DbSet<RolePlatformId> RolePlatformIds => Set<RolePlatformId>();

    public DbSet<BotStatus> BotStatuses => Set<BotStatus>();

    public DbSet<RoleSetting> RoleSettings => Set<RoleSetting>();

    public DbSet<RoleSettingChange> RoleSettingChanges => Set<RoleSettingChange>();

    public DbSet<TrackingPeriod> TrackingPeriods => Set<TrackingPeriod>();

    public DbSet<MessageEvent> MessageEvents => Set<MessageEvent>();

    public DbSet<MessageMention> MessageMentions => Set<MessageMention>();

    /// <summary>The web host's data protection keys (<c>PersistKeysToDbContext</c>).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>
    /// Options for a context outside dependency injection (design time, tests).
    /// </summary>
    public static DbContextOptions<ApolloDbContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<ApolloDbContext>()
            .UseNpgsql(connectionString)
            .UseTimescaleDb()
            .Options;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // CmdScale never emits CREATE EXTENSION; this makes the first migration do it (docs/adr/0001-timescale-ef.md).
        modelBuilder.HasPostgresExtension("timescaledb");

        IdentityModel.Configure(modelBuilder);
        GuildStateModel.Configure(modelBuilder);
        StatusModel.Configure(modelBuilder);
        TrackingModel.Configure(modelBuilder);
        MessageModel.Configure(modelBuilder);
        DataProtectionModel.Configure(modelBuilder);
    }
}
