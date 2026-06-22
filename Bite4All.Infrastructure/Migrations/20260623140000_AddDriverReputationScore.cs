using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bite4All.Infrastructure.Migrations;

public partial class AddDriverReputationScore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "ReputationScore",
            table: "Drivers",
            type: "float",
            nullable: false,
            defaultValue: 3.5);

        migrationBuilder.AddColumn<int>(
            name: "CompletedPickups",
            table: "Drivers",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "CancellationCount",
            table: "Drivers",
            type: "int",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ReputationScore",
            table: "Drivers");

        migrationBuilder.DropColumn(
            name: "CompletedPickups",
            table: "Drivers");

        migrationBuilder.DropColumn(
            name: "CancellationCount",
            table: "Drivers");
    }
}
