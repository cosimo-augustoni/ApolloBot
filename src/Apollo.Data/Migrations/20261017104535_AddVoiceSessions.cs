using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Apollo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "voice_sessions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    end_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voice_sessions", x => new { x.id, x.occurred_at });
                    table.ForeignKey(
                        name: "FK_voice_sessions_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voice_sessions_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voice_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "voice_state_segments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    self_mute = table.Column<bool>(type: "boolean", nullable: false),
                    self_deaf = table.Column<bool>(type: "boolean", nullable: false),
                    streaming = table.Column<bool>(type: "boolean", nullable: false),
                    video = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voice_state_segments", x => new { x.id, x.occurred_at });
                    table.ForeignKey(
                        name: "FK_voice_state_segments_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voice_state_segments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_voice_sessions_channel_id",
                table: "voice_sessions",
                column: "channel_id");

            migrationBuilder.CreateIndex(
                name: "IX_voice_sessions_guild_id",
                table: "voice_sessions",
                column: "guild_id");

            migrationBuilder.CreateIndex(
                name: "IX_voice_sessions_user_id",
                table: "voice_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_voice_state_segments_guild_id",
                table: "voice_state_segments",
                column: "guild_id");

            migrationBuilder.CreateIndex(
                name: "IX_voice_state_segments_session_id",
                table: "voice_state_segments",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_voice_state_segments_user_id",
                table: "voice_state_segments",
                column: "user_id");

            migrationBuilder.CreateHypertable(
                tableName: "voice_sessions",
                timeColumnName: "occurred_at",
                schema: "public",
                chunkTimeInterval: "7 days");

            migrationBuilder.CreateHypertable(
                tableName: "voice_state_segments",
                timeColumnName: "occurred_at",
                schema: "public",
                chunkTimeInterval: "7 days");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voice_sessions");

            migrationBuilder.DropTable(
                name: "voice_state_segments");
        }
    }
}
