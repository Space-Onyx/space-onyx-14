using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres;

[DbContext(typeof(PostgresServerDbContext))]
[Migration("20260809000001_OnyxLoadoutCustomization")]
public partial class OnyxLoadoutCustomization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "custom_color_tint", table: "profile_loadout", type: "character varying(16)", maxLength: 16, nullable: true);
        migrationBuilder.AddColumn<string>(name: "custom_name", table: "profile_loadout", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "custom_description", table: "profile_loadout", type: "text", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "custom_color_tint", table: "profile_loadout");
        migrationBuilder.DropColumn(name: "custom_name", table: "profile_loadout");
        migrationBuilder.DropColumn(name: "custom_description", table: "profile_loadout");
    }
}
