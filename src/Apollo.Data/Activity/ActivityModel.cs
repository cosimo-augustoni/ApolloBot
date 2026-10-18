using Apollo.Data.Messages;
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data.Activity;

/// <summary>
/// The activity aggregates the stats read (scope §5.3, #34). The Zurich-day aggregates are written by hand in the
/// <c>AddActivityAggregates</c> migration (ADR 0001) and mapped here with <c>ToView</c>, so EF never creates or diffs
/// them; their column names must match the aliases there. The hourly heatmap needs no time zone and is generated.
/// </summary>
internal static class ActivityModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyUserChannelActivity>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("daily_user_channel_activity");
            entity.Property(x => x.Day).HasColumnName("day");
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.ChannelId).HasColumnName("channel_id");
            entity.Property(x => x.ThreadParentId).HasColumnName("thread_parent_id");
            entity.Property(x => x.IsBot).HasColumnName("is_bot");
            entity.Property(x => x.MessageCount).HasColumnName("message_count");
            entity.Property(x => x.CharacterCount).HasColumnName("character_count");
            entity.Property(x => x.ReactionsGiven).HasColumnName("reactions_given");
            entity.Property(x => x.ReactionsReceived).HasColumnName("reactions_received");
            entity.Property(x => x.VoiceSeconds).HasColumnName("voice_seconds");
        });

        modelBuilder.Entity<DailyGuildActivity>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("daily_guild_activity");
            entity.Property(x => x.Day).HasColumnName("day");
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.IsBot).HasColumnName("is_bot");
            entity.Property(x => x.MessageCount).HasColumnName("message_count");
            entity.Property(x => x.CharacterCount).HasColumnName("character_count");
            entity.Property(x => x.ReactionCount).HasColumnName("reaction_count");
        });

        modelBuilder.Entity<HourlyActivityHeatmap>(entity =>
        {
            entity.HasNoKey();
            entity.Property(x => x.Hour).HasColumnName("hour");
            entity.Property(x => x.GuildId).HasColumnName("guild_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.IsBot).HasColumnName("is_bot");
            entity.Property(x => x.MessageCount).HasColumnName("message_count");

            // UTC and Zurich hours share their boundaries, so the fluent API's bucket without a time zone is right here
            // (ADR 0001, decision 3); the tests cover both DST switches.
            entity.IsContinuousAggregate<HourlyActivityHeatmap, MessageEvent>(
                    "hourly_activity_heatmap",
                    "1 hour",
                    x => x.OccurredAt)
                .WithTimeBucketProperty(x => x.Hour)
                .AddAggregateFunction(x => x.MessageCount, x => x.Id, EAggregateFunction.Count)
                .AddGroupByColumn(x => x.GuildId)
                .AddGroupByColumn(x => x.UserId)
                .AddGroupByColumn(x => x.IsBot)
                .Where("\"kind\" = 'Create'")
                .MaterializedOnly(false)
                .WithRefreshPolicy(
                    startOffset: ActivityAggregates.RefreshStartOffset,
                    endOffset: ActivityAggregates.RefreshEndOffset,
                    scheduleInterval: ActivityAggregates.RefreshScheduleInterval);
        });
    }
}
