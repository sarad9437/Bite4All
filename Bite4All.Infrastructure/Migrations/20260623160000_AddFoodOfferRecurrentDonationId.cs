using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bite4All.Infrastructure.Migrations;

public partial class AddFoodOfferRecurrentDonationId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Fix: add RecurrentDonationId so the scheduler can check per-recurrent-donation
        // rather than per-partner when deciding whether today's offer was already created.
        migrationBuilder.AddColumn<int>(
            name: "RecurrentDonationId",
            table: "FoodOffers",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_FoodOffers_RecurrentDonationId",
            table: "FoodOffers",
            column: "RecurrentDonationId");

        migrationBuilder.AddForeignKey(
            name: "FK_FoodOffers_RecurrentDonations_RecurrentDonationId",
            table: "FoodOffers",
            column: "RecurrentDonationId",
            principalTable: "RecurrentDonations",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_FoodOffers_RecurrentDonations_RecurrentDonationId",
            table: "FoodOffers");

        migrationBuilder.DropIndex(
            name: "IX_FoodOffers_RecurrentDonationId",
            table: "FoodOffers");

        migrationBuilder.DropColumn(
            name: "RecurrentDonationId",
            table: "FoodOffers");
    }
}
