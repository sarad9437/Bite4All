using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bite4All.Infrastructure.Migrations;

public partial class AddTemporaryCapacityExpiry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "TemporaryCapacityExpiresAtUtc",
            table: "CharityOrganizations",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "TemporaryCapacityExpiresAtUtc",
            table: "CharityOrganizations");
    }
}
