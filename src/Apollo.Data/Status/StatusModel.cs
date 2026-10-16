using Apollo.Data.Identity;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Status;

/// <summary>
/// The <c>bot_status</c> heartbeat hypertable. Its retention policy is #69.
/// </summary>
internal static class StatusModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BotStatus>(entity =>
        {
            entity.ToTable("bot_status");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.Connected).HasColumnName("connected");
            entity.Property(x => x.GatewayLatencyMs).HasColumnName("gateway_latency_ms");
            entity.Property(x => x.LastEventAt).HasColumnName("last_event_at");
            entity.Property(x => x.OpenVoiceSessions).HasColumnName("open_voice_sessions");

            // Hypertable rule: the primary key must contain the partition column (ADR 0001).
            entity.HasKey(x => new { x.Id, x.OccurredAt });

            // About 2,900 rows a day, so one chunk per month is plenty.
            entity.IsHypertable(x => x.OccurredAt)
                .WithChunkTimeInterval("30 days");

            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
