using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bite4All.Infrastructure.Migrations;

public partial class AddRecipientMealDistributions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RecipientMealDistributions",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                PickupDocumentId = table.Column<int>(type: "int", nullable: false),
                RecipientId = table.Column<int>(type: "int", nullable: false),
                CharityOrganizationId = table.Column<int>(type: "int", nullable: false),
                Category = table.Column<int>(type: "int", nullable: false),
                MealsCount = table.Column<int>(type: "int", nullable: false),
                DistributedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecipientMealDistributions", x => x.Id);
                table.ForeignKey(
                    name: "FK_RecipientMealDistributions_CharityOrganizations_CharityOrganizationId",
                    column: x => x.CharityOrganizationId,
                    principalTable: "CharityOrganizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_RecipientMealDistributions_PickupDocuments_PickupDocumentId",
                    column: x => x.PickupDocumentId,
                    principalTable: "PickupDocuments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_RecipientMealDistributions_Recipients_RecipientId",
                    column: x => x.RecipientId,
                    principalTable: "Recipients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RecipientMealDistributions_CharityOrganizationId",
            table: "RecipientMealDistributions",
            column: "CharityOrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_RecipientMealDistributions_PickupDocumentId_RecipientId",
            table: "RecipientMealDistributions",
            columns: new[] { "PickupDocumentId", "RecipientId" });

        migrationBuilder.CreateIndex(
            name: "IX_RecipientMealDistributions_RecipientId",
            table: "RecipientMealDistributions",
            column: "RecipientId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RecipientMealDistributions");
    }
}
