using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShortUrl.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ShortLinks",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Code = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 7, nullable: false),
                TargetUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                AccessCount = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ShortLinks", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ShortLinks_Code",
            table: "ShortLinks",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ShortLinks_ExpiresAtUtc",
            table: "ShortLinks",
            column: "ExpiresAtUtc");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ShortLinks");
    }
}

