using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Apollo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPresenceActivitySegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "presence_activity_segments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_type = table.Column<string>(type: "text", nullable: false),
                    activity_name = table.Column<string>(type: "text", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    details = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: true),
                    large_text = table.Column<string>(type: "text", nullable: true),
                    small_text = table.Column<string>(type: "text", nullable: true),
                    party_ext_id = table.Column<string>(type: "text", nullable: true),
                    party_size = table.Column<long>(type: "bigint", nullable: true),
                    party_max = table.Column<long>(type: "bigint", nullable: true),
                    application_ext_id = table.Column<string>(type: "text", nullable: true),
                    reported_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reported_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_presence_activity_segments", x => new { x.id, x.occurred_at });
                    table.ForeignKey(
                        name: "FK_presence_activity_segments_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_presence_activity_segments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_presence_activity_segments_guild_id_user_id_occurred_at",
                table: "presence_activity_segments",
                columns: new[] { "guild_id", "user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_presence_activity_segments_user_id",
                table: "presence_activity_segments",
                column: "user_id");

            migrationBuilder.CreateHypertable(
                tableName: "presence_activity_segments",
                timeColumnName: "occurred_at",
                schema: "public",
                chunkTimeInterval: "7 days");

            // Raw SQL: a data change, which CmdScale's fluent API does not cover. Every session stored before #115 gets
            // one segment covering it without rich fields (they were never recorded), so every session is covered by
            // its segments (scope.md §5.1).
            migrationBuilder.Sql(
                """
                INSERT INTO presence_activity_segments (guild_id, occurred_at, user_id, activity_type, activity_name, ended_at)
                SELECT guild_id, occurred_at, user_id, activity_type, activity_name, ended_at
                FROM presence_activity_sessions;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "presence_activity_segments");
        }
    }
}
