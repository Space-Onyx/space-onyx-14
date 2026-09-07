// Content taken from Wega (https://github.com/wega-team/ss14-wega), licensed under GNU GPL v3.
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres;

[DbContext(typeof(PostgresServerDbContext))]
[Migration("20260908000001_CharacterDescriptions")]
public partial class CharacterDescriptions : Migration
{
    private static readonly string[] Columns =
    [
        "oocflavor_text",
        "character_flavor_text",
        "tags_flavor_text",
        "links_flavor_text",
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var column in Columns)
            migrationBuilder.AddColumn<string>(
                name: column,
                table: "profile",
                type: "text",
                nullable: false,
                defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in Columns)
            migrationBuilder.DropColumn(name: column, table: "profile");
    }
}
