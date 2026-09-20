using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite;

[DbContext(typeof(SqliteServerDbContext))]
[Migration("20260920000000_SpeechBubbleReveal")]
public sealed class SpeechBubbleReveal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<float>(
            name: "speech_bubble_reveal_speed",
            table: "profile",
            type: "REAL",
            nullable: false,
            defaultValue: 20f);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "speech_bubble_reveal_speed",
            table: "profile");
    }
}
