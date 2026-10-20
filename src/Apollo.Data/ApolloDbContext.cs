using Apollo.Data.Activity;
using Apollo.Data.DataProtection;
using Apollo.Data.Guilds;
using Apollo.Data.Identity;
using Apollo.Data.Membership;
using Apollo.Data.Messages;
using Apollo.Data.Names;
using Apollo.Data.Presence;
using Apollo.Data.Reactions;
using Apollo.Data.Sessions;
using Apollo.Data.Status;
using Apollo.Data.Tracking;
using Apollo.Data.Voice;
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

    public DbSet<VoiceSession> VoiceSessions => Set<VoiceSession>();

    public DbSet<VoiceStateSegment> VoiceStateSegments => Set<VoiceStateSegment>();

    public DbSet<ReactionEvent> ReactionEvents => Set<ReactionEvent>();

    public DbSet<MembershipEvent> MembershipEvents => Set<MembershipEvent>();

    public DbSet<PresenceActivitySession> PresenceActivitySessions => Set<PresenceActivitySession>();

    public DbSet<PresenceActivitySegment> PresenceActivitySegments => Set<PresenceActivitySegment>();

    public DbSet<MemberNameChange> MemberNameChanges => Set<MemberNameChange>();

    public DbSet<UserSignOut> UserSignOuts => Set<UserSignOut>();

    /// <summary>Per Zurich day, user and channel (#34); read-only.</summary>
    public DbSet<DailyUserChannelActivity> DailyUserChannelActivity => Set<DailyUserChannelActivity>();

    /// <summary>Per Zurich day and guild (#34); read-only.</summary>
    public DbSet<DailyGuildActivity> DailyGuildActivity => Set<DailyGuildActivity>();

    /// <summary>Messages per hour and user (#34); read-only.</summary>
    public DbSet<HourlyActivityHeatmap> HourlyActivityHeatmap => Set<HourlyActivityHeatmap>();

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
        VoiceModel.Configure(modelBuilder);
        ReactionModel.Configure(modelBuilder);
        MembershipModel.Configure(modelBuilder);
        PresenceModel.Configure(modelBuilder);
        NameModel.Configure(modelBuilder);
        DataProtectionModel.Configure(modelBuilder);
        SessionModel.Configure(modelBuilder);
        ActivityModel.Configure(modelBuilder);
    }
}
