using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    public partial class DiscordAuthUniqueLinks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM discord_user WHERE discord_user_id NOT IN (SELECT MIN(discord_user_id) FROM discord_user GROUP BY user_id) OR discord_user_id NOT IN (SELECT MIN(discord_user_id) FROM discord_user GROUP BY discord_id)");

            migrationBuilder.CreateIndex(
                name: "IX_discord_user_discord_id",
                table: "discord_user",
                column: "discord_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_discord_user_user_id",
                table: "discord_user",
                column: "user_id",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_discord_user_discord_id", table: "discord_user");
            migrationBuilder.DropIndex(name: "IX_discord_user_user_id", table: "discord_user");
        }
    }
}
