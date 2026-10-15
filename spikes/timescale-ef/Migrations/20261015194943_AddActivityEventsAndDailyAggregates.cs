using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Apollo.Spikes.TimescaleEf.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityEventsAndDailyAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:timescaledb", ",,");

            migrationBuilder.CreateTable(
                name: "activity_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    channel_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_events", x => new { x.id, x.occurred_at });
                });

            migrationBuilder.CreateHypertable(
                tableName: "activity_events",
                timeColumnName: "occurred_at",
                schema: "public",
                chunkTimeInterval: "7 days",
                enableCompression: true,
                compressionSegmentBy: ["channel_id"]);

            migrationBuilder.CreateContinuousAggregate(
                materializedViewName: "daily_activity_utc",
                parentName: "activity_events",
                schema: "public",
                chunkInterval: "70 days",
                materializedOnly: true,
                timeBucketWidth: "1 day",
                timeBucketSourceColumn: "occurred_at",
                timeBucketColumnName: "day",
                aggregateFunctions: [
                    new CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions.ContinuousAggregateFunction("event_count", CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions.EAggregateFunction.Count, "id")
                ],
                groupByColumns: ["channel_id"]);

            migrationBuilder.AddContinuousAggregatePolicy(
                materializedViewName: "daily_activity_utc",
                schema: "public",
                startOffset: "3 days",
                endOffset: "1 hour",
                scheduleInterval: "1 hour");

            // Hand-written: CmdScale's structured aggregate always emits time_bucket(width, column) with no time zone
            // argument (ContinuousAggregateSqlGenerator, v10.4.0), which buckets by UTC day. viewDefinition is passed
            // through verbatim, so CmdScale still owns CREATE/DROP and the policy. Aliases must match the column names
            // configured for DailyActivityZurich in SpikeDbContext; the spike tests query every column through LINQ.
            migrationBuilder.CreateContinuousAggregate(
                materializedViewName: "daily_activity_zurich",
                parentName: "activity_events",
                schema: "public",
                chunkInterval: "70 days",
                materializedOnly: true,
                viewDefinition: """
                    SELECT time_bucket(INTERVAL '1 day', occurred_at, 'Europe/Zurich') AS day,
                           channel_id,
                           count(id) AS event_count
                    FROM public.activity_events
                    GROUP BY 1, channel_id
                    """);

            migrationBuilder.AddContinuousAggregatePolicy(
                materializedViewName: "daily_activity_zurich",
                schema: "public",
                startOffset: "3 days",
                endOffset: "1 hour",
                scheduleInterval: "1 hour");

            migrationBuilder.AddCompressionPolicy(
                tableName: "activity_events",
                schema: "public",
                after: "30 days");

            migrationBuilder.AddRetentionPolicy(
                tableName: "activity_events",
                schema: "public",
                dropAfter: "10 years",
                scheduleInterval: "1 day",
                maxRuntime: "00:00:00",
                maxRetries: -1,
                retryPeriod: "1 day");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropRetentionPolicy(
                tableName: "activity_events",
                schema: "public");

            migrationBuilder.RemoveContinuousAggregatePolicy(
                materializedViewName: "daily_activity_zurich",
                schema: "public",
                ifExists: true);

            migrationBuilder.DropContinuousAggregate(
                materializedViewName: "daily_activity_zurich",
                schema: "public");

            migrationBuilder.RemoveContinuousAggregatePolicy(
                materializedViewName: "daily_activity_utc",
                schema: "public",
                ifExists: true);

            migrationBuilder.DropCompressionPolicy(
                tableName: "activity_events",
                schema: "public");

            migrationBuilder.DropContinuousAggregate(
                materializedViewName: "daily_activity_utc",
                schema: "public");

            migrationBuilder.DropTable(
                name: "activity_events");
        }
    }
}
