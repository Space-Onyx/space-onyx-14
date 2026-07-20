using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    public partial class DiscordAuthUniqueLinks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM discord_user a USING discord_user b WHERE a.discord_user_id > b.discord_user_id AND (a.user_id = b.user_id OR a.discord_id = b.discord_id)");

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
