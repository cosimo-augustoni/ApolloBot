using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apollo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-written: CmdScale's structured aggregate always emits time_bucket(width, column) with no time zone
            // argument (ContinuousAggregateSqlGenerator, v10.4.0), which buckets by UTC day. viewDefinition is passed
            // through verbatim, so CmdScale still owns CREATE/DROP and the policy (docs/adr/0001-timescale-ef.md).
            // A continuous aggregate reads one hypertable, so there is one per source; the views below combine them.
            // "day" is the UTC instant of the Zurich midnight that starts the day. The aliases must match what the views
            // select; ActivityAggregatesTests reads every column through LINQ.
            migrationBuilder.CreateContinuousAggregate(
                materializedViewName: "daily_user_channel_messages",
                parentName: "message_events",
                schema: "public",
                materializedOnly: false,
                viewDefinition: """
                    -- Messages sent: Create rows only, so edits do not count and a deleted message still counts once.
                    SELECT time_bucket(INTERVAL '1 day', occurred_at, 'Europe/Zurich') AS day,
                           guild_id,
                           user_id,
                           channel_id,
                           is_bot,
                           count(*) AS message_count,
                           sum(length) AS character_count
                    FROM public.message_events
                    WHERE kind = 'Create'
                    GROUP BY 1, guild_id, user_id, channel_id, is_bot
                    """);

            migrationBuilder.AddContinuousAggregatePolicy(
                materializedViewName: "daily_user_channel_messages",
                schema: "public",
                startOffset: "7 days",
                endOffset: "1 hour",
                scheduleInterval: "30 minutes");

            migrationBuilder.CreateContinuousAggregate(
                materializedViewName: "daily_user_channel_reactions_given",
                parentName: "reaction_events",
                schema: "public",
                materializedOnly: false,
                viewDefinition: """
                    SELECT time_bucket(INTERVAL '1 day', occurred_at, 'Europe/Zurich') AS day,
                           guild_id,
                           user_id,
                           channel_id,
                           count(*) AS reaction_count
                    FROM public.reaction_events
                    WHERE kind = 'Add'
                    GROUP BY 1, guild_id, user_id, channel_id
                    """);

            migrationBuilder.AddContinuousAggregatePolicy(
                materializedViewName: "daily_user_channel_reactions_given",
                schema: "public",
                startOffset: "7 days",
                endOffset: "1 hour",
                scheduleInterval: "30 minutes");

            migrationBuilder.CreateContinuousAggregate(
                materializedViewName: "daily_user_channel_reactions_received",
                parentName: "reaction_events",
                schema: "public",
                materializedOnly: false,
                viewDefinition: """
                    -- Reactions on a tracked author's messages; NULL authors (unknown or untracked) are left out.
                    SELECT time_bucket(INTERVAL '1 day', occurred_at, 'Europe/Zurich') AS day,
                           guild_id,
                           message_author_id AS user_id,
                           channel_id,
                           count(*) AS reaction_count
                    FROM public.reaction_events
                    WHERE kind = 'Add' AND message_author_id IS NOT NULL
                    GROUP BY 1, guild_id, message_author_id, channel_id
                    """);

            migrationBuilder.AddContinuousAggregatePolicy(
                materializedViewName: "daily_user_channel_reactions_received",
                schema: "public",
                startOffset: "7 days",
                endOffset: "1 hour",
                scheduleInterval: "30 minutes");

            migrationBuilder.CreateContinuousAggregate(
                materializedViewName: "daily_user_channel_voice",
                parentName: "voice_sessions",
                schema: "public",
                materializedOnly: false,
                viewDefinition: """
                    -- Closed sessions only. A session counts whole on the Zurich day it started (occurred_at), also when it
                    -- runs past midnight; closing it updates ended_at, which invalidates that day for the next refresh.
                    SELECT time_bucket(INTERVAL '1 day', occurred_at, 'Europe/Zurich') AS day,
                           guild_id,
                           user_id,
                           channel_id,
                           sum(EXTRACT(EPOCH FROM ended_at - occurred_at)) AS voice_seconds
                    FROM public.voice_sessions
                    WHERE ended_at IS NOT NULL
                    GROUP BY 1, guild_id, user_id, channel_id
                    """);

            migrationBuilder.AddContinuousAggregatePolicy(
                materializedViewName: "daily_user_channel_voice",
                schema: "public",
                startOffset: "7 days",
                endOffset: "1 hour",
                scheduleInterval: "30 minutes");

            migrationBuilder.CreateContinuousAggregate(
                materializedViewName: "hourly_activity_heatmap",
                parentName: "message_events",
                schema: "public",
                timeBucketWidth: "1 hour",
                timeBucketSourceColumn: "occurred_at",
                timeBucketColumnName: "hour",
                aggregateFunctions: [
                    new CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions.ContinuousAggregateFunction("message_count", CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions.EAggregateFunction.Count, "id")
                ],
                groupByColumns: ["guild_id", "user_id", "is_bot"],
                whereClause: "\"kind\" = 'Create'");

            migrationBuilder.AddContinuousAggregatePolicy(
                materializedViewName: "hourly_activity_heatmap",
                schema: "public",
                startOffset: "7 days",
                endOffset: "1 hour",
                scheduleInterval: "30 minutes");

            // Raw SQL: plain views, which neither CmdScale nor EF creates (a ToView entity is never migrated). They combine
            // the per-source aggregates at query time and take thread_parent_id from channels and is_bot from
            // guild_members there as well (a continuous aggregate would freeze them at refresh time), so a later sync
            // fix shows up without re-materializing. Read through DailyUserChannelActivity and DailyGuildActivity.
            migrationBuilder.Sql("""
                    CREATE VIEW public.daily_user_channel_activity AS
                    SELECT a.day,
                           a.guild_id,
                           a.user_id,
                           a.channel_id,
                           c.thread_parent_id,
                           coalesce(bool_or(a.is_bot), false) OR coalesce(m.is_bot, false) AS is_bot,
                           coalesce(sum(a.message_count), 0)::bigint AS message_count,
                           coalesce(sum(a.character_count), 0)::bigint AS character_count,
                           coalesce(sum(a.reactions_given), 0)::bigint AS reactions_given,
                           coalesce(sum(a.reactions_received), 0)::bigint AS reactions_received,
                           coalesce(round(sum(a.voice_seconds)), 0)::bigint AS voice_seconds
                    FROM (
                        SELECT day, guild_id, user_id, channel_id, is_bot, message_count, character_count,
                               NULL::bigint AS reactions_given, NULL::bigint AS reactions_received, NULL::numeric AS voice_seconds
                        FROM public.daily_user_channel_messages
                        UNION ALL
                        SELECT day, guild_id, user_id, channel_id, NULL, NULL, NULL, reaction_count, NULL, NULL
                        FROM public.daily_user_channel_reactions_given
                        UNION ALL
                        SELECT day, guild_id, user_id, channel_id, NULL, NULL, NULL, NULL, reaction_count, NULL
                        FROM public.daily_user_channel_reactions_received
                        UNION ALL
                        SELECT day, guild_id, user_id, channel_id, NULL, NULL, NULL, NULL, NULL, voice_seconds
                        FROM public.daily_user_channel_voice
                    ) a
                    LEFT JOIN public.channels c ON c.id = a.channel_id
                    LEFT JOIN public.guild_members m ON m.guild_id = a.guild_id AND m.user_id = a.user_id
                    GROUP BY a.day, a.guild_id, a.user_id, a.channel_id, c.thread_parent_id, m.is_bot;
                    """);

            migrationBuilder.Sql("""
                    CREATE VIEW public.daily_guild_activity AS
                    SELECT day,
                           guild_id,
                           is_bot,
                           sum(message_count)::bigint AS message_count,
                           sum(character_count)::bigint AS character_count,
                           sum(reactions_given)::bigint AS reaction_count
                    FROM public.daily_user_channel_activity
                    GROUP BY day, guild_id, is_bot;
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Raw SQL: see Up. The views depend on the aggregates, so they go first.
            migrationBuilder.Sql("DROP VIEW public.daily_guild_activity;");
            migrationBuilder.Sql("DROP VIEW public.daily_user_channel_activity;");

            migrationBuilder.RemoveContinuousAggregatePolicy(
                materializedViewName: "daily_user_channel_voice",
                schema: "public",
                ifExists: true);

            migrationBuilder.DropContinuousAggregate(
                materializedViewName: "daily_user_channel_voice",
                schema: "public");

            migrationBuilder.RemoveContinuousAggregatePolicy(
                materializedViewName: "daily_user_channel_reactions_received",
                schema: "public",
                ifExists: true);

            migrationBuilder.DropContinuousAggregate(
                materializedViewName: "daily_user_channel_reactions_received",
                schema: "public");

            migrationBuilder.RemoveContinuousAggregatePolicy(
                materializedViewName: "daily_user_channel_reactions_given",
                schema: "public",
                ifExists: true);

            migrationBuilder.DropContinuousAggregate(
                materializedViewName: "daily_user_channel_reactions_given",
                schema: "public");

            migrationBuilder.RemoveContinuousAggregatePolicy(
                materializedViewName: "daily_user_channel_messages",
                schema: "public",
                ifExists: true);

            migrationBuilder.DropContinuousAggregate(
                materializedViewName: "daily_user_channel_messages",
                schema: "public");

            migrationBuilder.RemoveContinuousAggregatePolicy(
                materializedViewName: "hourly_activity_heatmap",
                schema: "public",
                ifExists: true);

            migrationBuilder.DropContinuousAggregate(
                materializedViewName: "hourly_activity_heatmap",
                schema: "public");
        }
    }
}
