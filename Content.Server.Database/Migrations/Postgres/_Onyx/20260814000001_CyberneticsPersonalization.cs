using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres;

[DbContext(typeof(PostgresServerDbContext))]
[Migration("20260814000001_CyberneticsPersonalization")]
public partial class CyberneticsPersonalization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string[]>(
            name: "cybernetic_ids",
            table: "profile",
            type: "text[]",
            nullable: false,
            defaultValue: System.Array.Empty<string>());
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "cybernetic_ids", table: "profile");
    }
}
