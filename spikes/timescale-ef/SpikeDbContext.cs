using CmdScale.EntityFrameworkCore.TimescaleDB;
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Spikes.TimescaleEf;

public sealed class SpikeDbContext(DbContextOptions<SpikeDbContext> options) : DbContext(options)
{
    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();

    public DbSet<DailyActivityUtc> DailyActivityUtc => Set<DailyActivityUtc>();

    public DbSet<DailyActivityZurich> DailyActivityZurich => Set<DailyActivityZurich>();

    public static DbContextOptions<SpikeDbContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<SpikeDbContext>()
            .UseNpgsql(connectionString)
            .UseTimescaleDb()
            .Options;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Emits CREATE EXTENSION IF NOT EXISTS; CmdScale itself never creates the extension.
        modelBuilder.HasPostgresExtension("timescaledb");

        modelBuilder.Entity<ActivityEvent>(entity =>
        {
            entity.ToTable("activity_events");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.ChannelId).HasColumnName("channel_id");

            // Hypertable rule: every unique index, the primary key included, must contain the partition column.
            entity.HasKey(x => new { x.Id, x.OccurredAt });

            entity.IsHypertable(x => x.OccurredAt)
                .WithChunkTimeInterval("7 days");

            entity.WithCompressionSegmentBy(x => x.ChannelId);
            entity.WithCompressionPolicy(after: "30 days");
            entity.WithRetentionPolicy(dropAfter: "10 years");
        });

        modelBuilder.Entity<DailyActivityUtc>(entity =>
        {
            entity.HasNoKey();
            entity.Property(x => x.Day).HasColumnName("day");
            entity.Property(x => x.ChannelId).HasColumnName("channel_id");
            entity.Property(x => x.EventCount).HasColumnName("event_count");

            entity.IsContinuousAggregate<DailyActivityUtc, ActivityEvent>(
                    "daily_activity_utc",
                    "1 day",
                    x => x.OccurredAt,
                    chunkInterval: "70 days")
                .WithTimeBucketProperty(x => x.Day)
                .AddAggregateFunction(x => x.EventCount, x => x.Id, EAggregateFunction.Count)
                .AddGroupByColumn(x => x.ChannelId)
                .MaterializedOnly(true)
                .WithRefreshPolicy(startOffset: "3 days", endOffset: "1 hour", scheduleInterval: "1 hour");
        });

        modelBuilder.Entity<DailyActivityZurich>(entity =>
        {
            // ToView keeps EF from creating a table; the continuous aggregate comes from the migration.
            // Column names here must match the aliases in the migration's view definition.
            entity.HasNoKey();
            entity.ToView("daily_activity_zurich");
            entity.Property(x => x.Day).HasColumnName("day");
            entity.Property(x => x.ChannelId).HasColumnName("channel_id");
            entity.Property(x => x.EventCount).HasColumnName("event_count");
        });
    }
}
