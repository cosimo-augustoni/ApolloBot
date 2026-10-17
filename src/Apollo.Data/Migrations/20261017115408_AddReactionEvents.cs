using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Apollo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReactionEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reaction_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_ext_id = table.Column<string>(type: "text", nullable: false),
                    message_author_id = table.Column<Guid>(type: "uuid", nullable: true),
                    emoji_name = table.Column<string>(type: "text", nullable: false),
                    emoji_custom_id = table.Column<string>(type: "text", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reaction_events", x => new { x.id, x.occurred_at });
                    table.CheckConstraint("CK_reaction_events_unicode_emoji", "emoji_custom_id IS NOT NULL OR emoji_name <> ''");
                    table.ForeignKey(
                        name: "FK_reaction_events_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reaction_events_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reaction_events_users_message_author_id",
                        column: x => x.message_author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reaction_events_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reaction_events_channel_id",
                table: "reaction_events",
                column: "channel_id");

            migrationBuilder.CreateIndex(
                name: "IX_reaction_events_guild_id_message_ext_id",
                table: "reaction_events",
                columns: new[] { "guild_id", "message_ext_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reaction_events_message_author_id",
                table: "reaction_events",
                column: "message_author_id");

            migrationBuilder.CreateIndex(
                name: "IX_reaction_events_user_id",
                table: "reaction_events",
                column: "user_id");

            migrationBuilder.CreateHypertable(
                tableName: "reaction_events",
                timeColumnName: "occurred_at",
                schema: "public",
                chunkTimeInterval: "7 days");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reaction_events");
        }
    }
}
