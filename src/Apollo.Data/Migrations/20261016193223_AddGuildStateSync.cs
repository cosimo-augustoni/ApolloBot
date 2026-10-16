using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apollo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildStateSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "avatar_url",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "color",
                table: "roles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_administrator",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "roles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "position",
                table: "roles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "guilds",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                table: "guilds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "channels",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "channels",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "channels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "thread_parent_id",
                table: "channels",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "channels",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "guild_members",
                columns: table => new
                {
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_administrator = table.Column<bool>(type: "boolean", nullable: false),
                    is_bot = table.Column<bool>(type: "boolean", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guild_members", x => new { x.guild_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_guild_members_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_guild_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_roles",
                columns: table => new
                {
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_roles", x => new { x.guild_id, x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_member_roles_guild_members_guild_id_user_id",
                        columns: x => new { x.guild_id, x.user_id },
                        principalTable: "guild_members",
                        principalColumns: new[] { "guild_id", "user_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_member_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guilds_owner_user_id",
                table: "guilds",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_channels_category_id",
                table: "channels",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_channels_thread_parent_id",
                table: "channels",
                column: "thread_parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_guild_members_user_id",
                table: "guild_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_member_roles_role_id",
                table: "member_roles",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "FK_channels_channels_category_id",
                table: "channels",
                column: "category_id",
                principalTable: "channels",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_channels_channels_thread_parent_id",
                table: "channels",
                column: "thread_parent_id",
                principalTable: "channels",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_guilds_users_owner_user_id",
                table: "guilds",
                column: "owner_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_channels_channels_category_id",
                table: "channels");

            migrationBuilder.DropForeignKey(
                name: "FK_channels_channels_thread_parent_id",
                table: "channels");

            migrationBuilder.DropForeignKey(
                name: "FK_guilds_users_owner_user_id",
                table: "guilds");

            migrationBuilder.DropTable(
                name: "member_roles");

            migrationBuilder.DropTable(
                name: "guild_members");

            migrationBuilder.DropIndex(
                name: "IX_guilds_owner_user_id",
                table: "guilds");

            migrationBuilder.DropIndex(
                name: "IX_channels_category_id",
                table: "channels");

            migrationBuilder.DropIndex(
                name: "IX_channels_thread_parent_id",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "avatar_url",
                table: "users");

            migrationBuilder.DropColumn(
                name: "display_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "color",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "is_administrator",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "name",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "position",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "name",
                table: "guilds");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "guilds");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "name",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "thread_parent_id",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "type",
                table: "channels");
        }
    }
}
