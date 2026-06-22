using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bite4All.Infrastructure.Migrations;

public partial class AddReputationSnapshotsAndMatchNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "NotifiedAtUtc",
            table: "OfferMatches",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ReputationSnapshots",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ActorType = table.Column<int>(type: "int", nullable: false),
                ActorId = table.Column<int>(type: "int", nullable: false),
                Score = table.Column<double>(type: "float", nullable: false),
                Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReputationSnapshots", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ReputationSnapshots_ActorType_ActorId_CreatedAtUtc",
            table: "ReputationSnapshots",
            columns: new[] { "ActorType", "ActorId", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ReputationSnapshots");

        migrationBuilder.DropColumn(
            name: "NotifiedAtUtc",
            table: "OfferMatches");
    }
}
