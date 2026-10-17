using Apollo.Data.Identity;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Voice;

/// <summary>
/// The <c>voice_sessions</c> and <c>voice_state_segments</c> hypertables (#16).
/// </summary>
internal static class VoiceModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VoiceSession>(entity =>
        {
            entity.ToTable("voice_sessions");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.ChannelId).HasColumnName("channel_id");
            entity.Property(x => x.EndedAt).HasColumnName("ended_at");
            entity.Property(x => x.EndReason).HasColumnName("end_reason").HasConversion<string>();

            // Hypertable rule: the primary key must contain the partition column (ADR 0001).
            entity.HasKey(x => new { x.Id, x.OccurredAt });
            entity.IsHypertable(x => x.OccurredAt);

            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Channel>().WithMany().HasForeignKey(x => x.ChannelId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VoiceStateSegment>(entity =>
        {
            entity.ToTable("voice_state_segments");
            entity.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.SessionId).HasColumnName("session_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.EndedAt).HasColumnName("ended_at");
            entity.Property(x => x.SelfMute).HasColumnName("self_mute");
            entity.Property(x => x.SelfDeaf).HasColumnName("self_deaf");
            entity.Property(x => x.Streaming).HasColumnName("streaming");
            entity.Property(x => x.Video).HasColumnName("video");

            entity.HasKey(x => new { x.Id, x.OccurredAt });
            entity.IsHypertable(x => x.OccurredAt);

            // No foreign key to voice_sessions: its primary key is (id, occurred_at), so a reference would have to carry
            // the session's start as well. session_id is indexed for the join instead.
            entity.HasIndex(x => x.SessionId);

            entity.HasOne<Guild>().WithMany().HasForeignKey(x => x.GuildId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
