using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bite4All.Infrastructure.Migrations;

public partial class AddFoodOfferMatchResponseWindow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "MatchResponseWindowMinutes",
            table: "FoodOffers",
            type: "int",
            nullable: false,
            defaultValue: 30);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MatchResponseWindowMinutes",
            table: "FoodOffers");
    }
}
