using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite;

[DbContext(typeof(SqliteServerDbContext))]
[Migration("20260814000000_CyberneticsPersonalization")]
public partial class CyberneticsPersonalization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "cybernetic_ids",
            table: "profile",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "cybernetic_ids", table: "profile");
    }
}
