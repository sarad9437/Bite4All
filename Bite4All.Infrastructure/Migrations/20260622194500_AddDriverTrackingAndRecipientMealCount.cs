using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bite4All.Infrastructure.Migrations;

public partial class AddDriverTrackingAndRecipientMealCount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "CurrentLatitude",
            table: "Drivers",
            type: "decimal(9,6)",
            precision: 9,
            scale: 6,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "CurrentLongitude",
            table: "Drivers",
            type: "decimal(9,6)",
            precision: 9,
            scale: 6,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LocationUpdatedAtUtc",
            table: "Drivers",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "MealsReceivedCount",
            table: "Recipients",
            type: "int",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CurrentLatitude",
            table: "Drivers");

        migrationBuilder.DropColumn(
            name: "CurrentLongitude",
            table: "Drivers");

        migrationBuilder.DropColumn(
            name: "LocationUpdatedAtUtc",
            table: "Drivers");

        migrationBuilder.DropColumn(
            name: "MealsReceivedCount",
            table: "Recipients");
    }
}
