using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apollo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleSettingsPage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "manages_roles",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "bot_user_id",
                table: "guild_platform_ids",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_guild_platform_ids_bot_user_id",
                table: "guild_platform_ids",
                column: "bot_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_guild_platform_ids_users_bot_user_id",
                table: "guild_platform_ids",
                column: "bot_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Raw SQL: EF Core and CmdScale cannot define triggers. Replaces the #14 trigger function so a removal names
            // its author and time too (#33): RoleSettingsStore sets both as transaction-local settings before it deletes.
            // A delete without them (by hand, or a role cleared outside the admin page) still records no author and the
            // database clock.
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION role_settings_audit() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    removed_by uuid := NULLIF(current_setting('apollo.role_settings_changed_by', true), '')::uuid;
                    removed_at timestamptz := COALESCE(NULLIF(current_setting('apollo.role_settings_changed_at', true), '')::timestamptz, now());
                BEGIN
                    IF TG_OP IN ('UPDATE', 'DELETE') AND (TG_OP = 'DELETE' OR (OLD.guild_id, OLD.platform, OLD.purpose, OLD.role_id)
                            IS DISTINCT FROM (NEW.guild_id, NEW.platform, NEW.purpose, NEW.role_id)) THEN
                        INSERT INTO role_setting_changes (guild_id, platform, purpose, role_id, action, changed_by_user_id, changed_at)
                        VALUES (OLD.guild_id, OLD.platform, OLD.purpose, OLD.role_id, 'Removed',
                                CASE WHEN TG_OP = 'UPDATE' THEN NEW.changed_by_user_id ELSE removed_by END,
                                CASE WHEN TG_OP = 'UPDATE' THEN NEW.changed_at ELSE removed_at END);
                    END IF;

                    IF TG_OP = 'INSERT' OR (TG_OP = 'UPDATE' AND (OLD.guild_id, OLD.platform, OLD.purpose, OLD.role_id)
                            IS DISTINCT FROM (NEW.guild_id, NEW.platform, NEW.purpose, NEW.role_id)) THEN
                        INSERT INTO role_setting_changes (guild_id, platform, purpose, role_id, action, changed_by_user_id, changed_at)
                        VALUES (NEW.guild_id, NEW.platform, NEW.purpose, NEW.role_id, 'Added', NEW.changed_by_user_id, NEW.changed_at);
                    END IF;

                    RETURN NULL;
                END;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The #14 trigger function (AddTrackingGate): a delete names no author.
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION role_settings_audit() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP IN ('UPDATE', 'DELETE') AND (TG_OP = 'DELETE' OR (OLD.guild_id, OLD.platform, OLD.purpose, OLD.role_id)
                            IS DISTINCT FROM (NEW.guild_id, NEW.platform, NEW.purpose, NEW.role_id)) THEN
                        INSERT INTO role_setting_changes (guild_id, platform, purpose, role_id, action, changed_by_user_id, changed_at)
                        VALUES (OLD.guild_id, OLD.platform, OLD.purpose, OLD.role_id, 'Removed',
                                CASE WHEN TG_OP = 'UPDATE' THEN NEW.changed_by_user_id END,
                                CASE WHEN TG_OP = 'UPDATE' THEN NEW.changed_at ELSE now() END);
                    END IF;

                    IF TG_OP = 'INSERT' OR (TG_OP = 'UPDATE' AND (OLD.guild_id, OLD.platform, OLD.purpose, OLD.role_id)
                            IS DISTINCT FROM (NEW.guild_id, NEW.platform, NEW.purpose, NEW.role_id)) THEN
                        INSERT INTO role_setting_changes (guild_id, platform, purpose, role_id, action, changed_by_user_id, changed_at)
                        VALUES (NEW.guild_id, NEW.platform, NEW.purpose, NEW.role_id, 'Added', NEW.changed_by_user_id, NEW.changed_at);
                    END IF;

                    RETURN NULL;
                END;
                $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_guild_platform_ids_users_bot_user_id",
                table: "guild_platform_ids");

            migrationBuilder.DropIndex(
                name: "IX_guild_platform_ids_bot_user_id",
                table: "guild_platform_ids");

            migrationBuilder.DropColumn(
                name: "manages_roles",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "bot_user_id",
                table: "guild_platform_ids");
        }
    }
}
