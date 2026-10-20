using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Apollo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberNameChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "member_name_changes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nickname = table.Column<string>(type: "text", nullable: true),
                    global_name = table.Column<string>(type: "text", nullable: true),
                    username = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_name_changes", x => new { x.id, x.occurred_at });
                    table.ForeignKey(
                        name: "FK_member_name_changes_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_member_name_changes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_member_name_changes_guild_id_user_id_occurred_at",
                table: "member_name_changes",
                columns: new[] { "guild_id", "user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_member_name_changes_user_id",
                table: "member_name_changes",
                column: "user_id");

            migrationBuilder.CreateHypertable(
                tableName: "member_name_changes",
                timeColumnName: "occurred_at",
                schema: "public",
                chunkTimeInterval: "7 days");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_name_changes");
        }
    }
}
