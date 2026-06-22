using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bite4All.Infrastructure.Migrations;

public partial class AddIdempotencyRecords : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IdempotencyRecords",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RouteKey = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                RequestHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                StatusCode = table.Column<int>(type: "int", nullable: true),
                ResponseContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_IdempotencyRecords_ExpiresAtUtc",
            table: "IdempotencyRecords",
            column: "ExpiresAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_IdempotencyRecords_RouteKey",
            table: "IdempotencyRecords",
            column: "RouteKey",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "IdempotencyRecords");
    }
}
