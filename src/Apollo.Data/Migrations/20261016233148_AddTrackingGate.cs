using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Apollo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackingGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_setting_changes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "text", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_setting_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_setting_changes_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_setting_changes_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_setting_changes_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_settings",
                columns: table => new
                {
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "text", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_settings", x => new { x.guild_id, x.platform, x.purpose, x.role_id });
                    table.ForeignKey(
                        name: "FK_role_settings_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_settings_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_settings_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tracking_periods",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    guild_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracking_periods", x => x.id);
                    table.ForeignKey(
                        name: "FK_tracking_periods_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tracking_periods_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_role_setting_changes_changed_by_user_id",
                table: "role_setting_changes",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_setting_changes_guild_id_platform_purpose",
                table: "role_setting_changes",
                columns: new[] { "guild_id", "platform", "purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_role_setting_changes_role_id",
                table: "role_setting_changes",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_settings_changed_by_user_id",
                table: "role_settings",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_settings_role_id",
                table: "role_settings",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ux_role_settings_one_tracking_role",
                table: "role_settings",
                columns: new[] { "guild_id", "platform" },
                unique: true,
                filter: "purpose = 'Tracking'");

            migrationBuilder.CreateIndex(
                name: "IX_tracking_periods_user_id",
                table: "tracking_periods",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_tracking_periods_one_open_period",
                table: "tracking_periods",
                columns: new[] { "guild_id", "user_id" },
                unique: true,
                filter: "\"to\" IS NULL");

            // Raw SQL: EF Core and CmdScale cannot define triggers. A trigger (rather than SaveChanges logic) audits every
            // insert, update and delete of role_settings in the same transaction, including rows inserted by hand in M1,
            // so the "was a tracking role ever configured" check can never be bypassed (#14). A delete names no author.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION role_settings_audit() RETURNS trigger
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

                CREATE TRIGGER role_settings_audit
                AFTER INSERT OR UPDATE OR DELETE ON role_settings
                FOR EACH ROW EXECUTE FUNCTION role_settings_audit();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER role_settings_audit ON role_settings;
                DROP FUNCTION role_settings_audit();
                """);

            migrationBuilder.DropTable(
                name: "role_setting_changes");

            migrationBuilder.DropTable(
                name: "role_settings");

            migrationBuilder.DropTable(
                name: "tracking_periods");
        }
    }
}
