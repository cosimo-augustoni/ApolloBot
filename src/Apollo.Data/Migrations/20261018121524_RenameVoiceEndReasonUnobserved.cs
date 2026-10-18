using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apollo.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameVoiceEndReasonUnobserved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Raw SQL: a data rewrite, not a schema change, so there is nothing for EF or CmdScale to generate. #98
            // renamed the end reason BotRestart to Unobserved (it marks every end the bot did not see), and production
            // already has rows with the old value, which the enum conversion could no longer read.
            migrationBuilder.Sql("UPDATE voice_sessions SET end_reason = 'Unobserved' WHERE end_reason = 'BotRestart';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Raw SQL: see Up. Rows the new code closed as Unobserved after a failed save or a reconnect go back to the
            // only value the old code knows.
            migrationBuilder.Sql("UPDATE voice_sessions SET end_reason = 'BotRestart' WHERE end_reason = 'Unobserved';");
        }
    }
}
