using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Apollo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageEventsAndMentions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "message_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    thread_parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message_ext_id = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    length = table.Column<int>(type: "integer", nullable: true),
                    word_count = table.Column<int>(type: "integer", nullable: true),
                    attachment_count = table.Column<int>(type: "integer", nullable: true),
                    embed_count = table.Column<int>(type: "integer", nullable: true),
                    emoji_count = table.Column<int>(type: "integer", nullable: true),
                    user_mention_count = table.Column<int>(type: "integer", nullable: true),
                    role_mention_count = table.Column<int>(type: "integer", nullable: true),
                    mentions_everyone = table.Column<bool>(type: "boolean", nullable: true),
                    content_parts = table.Column<short>(type: "smallint", nullable: true),
                    reply_to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_bot = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_events", x => new { x.id, x.occurred_at });
                    table.ForeignKey(
                        name: "FK_message_events_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_message_events_channels_thread_parent_id",
                        column: x => x.thread_parent_id,
                        principalTable: "channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_message_events_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_message_events_users_reply_to_user_id",
                        column: x => x.reply_to_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_message_events_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "message_mentions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_ext_id = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mentioned_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_mentions", x => new { x.id, x.occurred_at });
                    table.ForeignKey(
                        name: "FK_message_mentions_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_message_mentions_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_message_mentions_users_mentioned_user_id",
                        column: x => x.mentioned_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_message_mentions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_message_events_channel_id",
                table: "message_events",
                column: "channel_id");

            migrationBuilder.CreateIndex(
                name: "IX_message_events_guild_id_message_ext_id",
                table: "message_events",
                columns: new[] { "guild_id", "message_ext_id" });

            migrationBuilder.CreateIndex(
                name: "IX_message_events_reply_to_user_id",
                table: "message_events",
                column: "reply_to_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_message_events_thread_parent_id",
                table: "message_events",
                column: "thread_parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_message_events_user_id",
                table: "message_events",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_message_mentions_channel_id",
                table: "message_mentions",
                column: "channel_id");

            migrationBuilder.CreateIndex(
                name: "IX_message_mentions_guild_id",
                table: "message_mentions",
                column: "guild_id");

            migrationBuilder.CreateIndex(
                name: "IX_message_mentions_mentioned_user_id",
                table: "message_mentions",
                column: "mentioned_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_message_mentions_user_id",
                table: "message_mentions",
                column: "user_id");

            migrationBuilder.CreateHypertable(
                tableName: "message_events",
                timeColumnName: "occurred_at",
                schema: "public",
                chunkTimeInterval: "7 days");

            migrationBuilder.CreateHypertable(
                tableName: "message_mentions",
                timeColumnName: "occurred_at",
                schema: "public",
                chunkTimeInterval: "7 days");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "message_events");

            migrationBuilder.DropTable(
                name: "message_mentions");
        }
    }
}
