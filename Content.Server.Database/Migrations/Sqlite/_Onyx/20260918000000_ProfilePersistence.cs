// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite;

[DbContext(typeof(SqliteServerDbContext))]
[Migration("20260918000000_ProfilePersistence")]
public partial class ProfilePersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "bark_proto",
            table: "profile",
            type: "TEXT",
            nullable: false,
            defaultValue: "Human1");
        migrationBuilder.AddColumn<float>(
            name: "bark_pitch",
            table: "profile",
            type: "REAL",
            nullable: false,
            defaultValue: 1f);
        migrationBuilder.AddColumn<float>(
            name: "bark_min_var",
            table: "profile",
            type: "REAL",
            nullable: false,
            defaultValue: 0.1f);
        migrationBuilder.AddColumn<float>(
            name: "bark_max_var",
            table: "profile",
            type: "REAL",
            nullable: false,
            defaultValue: 0.5f);
        migrationBuilder.AddColumn<string>(
            name: "synthetic_law_preset",
            table: "profile_role_loadout",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "bark_proto", table: "profile");
        migrationBuilder.DropColumn(name: "bark_pitch", table: "profile");
        migrationBuilder.DropColumn(name: "bark_min_var", table: "profile");
        migrationBuilder.DropColumn(name: "bark_max_var", table: "profile");
        migrationBuilder.DropColumn(name: "synthetic_law_preset", table: "profile_role_loadout");
    }
}
