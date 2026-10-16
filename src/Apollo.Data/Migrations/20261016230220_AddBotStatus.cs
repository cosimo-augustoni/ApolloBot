using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Apollo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBotStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bot_status",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connected = table.Column<bool>(type: "boolean", nullable: false),
                    gateway_latency_ms = table.Column<int>(type: "integer", nullable: true),
                    last_event_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    open_voice_sessions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_status", x => new { x.id, x.occurred_at });
                    table.ForeignKey(
                        name: "FK_bot_status_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bot_status_guild_id",
                table: "bot_status",
                column: "guild_id");

            migrationBuilder.CreateHypertable(
                tableName: "bot_status",
                timeColumnName: "occurred_at",
                schema: "public",
                chunkTimeInterval: "30 days");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bot_status");
        }
    }
}
