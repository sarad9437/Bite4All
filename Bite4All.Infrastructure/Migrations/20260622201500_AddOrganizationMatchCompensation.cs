using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bite4All.Infrastructure.Migrations;

public partial class AddOrganizationMatchCompensation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "MatchCompensationBonus",
            table: "CharityOrganizations",
            type: "decimal(10,2)",
            precision: 10,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<DateTime>(
            name: "MatchCompensationExpiresAtUtc",
            table: "CharityOrganizations",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MatchCompensationBonus",
            table: "CharityOrganizations");

        migrationBuilder.DropColumn(
            name: "MatchCompensationExpiresAtUtc",
            table: "CharityOrganizations");
    }
}
