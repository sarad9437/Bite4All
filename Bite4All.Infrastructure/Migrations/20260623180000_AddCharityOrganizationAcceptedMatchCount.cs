using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bite4All.Infrastructure.Migrations;

/// <summary>
/// Adds AcceptedMatchCount and CancellationCount to CharityOrganizations.
/// These fields exist in the domain entity and are used for reputation scoring,
/// but no migration was present — the table would be missing these columns on a
/// fresh database deployment.
/// </summary>
public partial class AddCharityOrganizationAcceptedMatchCount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AcceptedMatchCount",
            table: "CharityOrganizations",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "CancellationCount",
            table: "CharityOrganizations",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<double>(
            name: "ReputationScore",
            table: "CharityOrganizations",
            type: "float",
            nullable: false,
            defaultValue: 3.5);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastReceivedAtUtc",
            table: "CharityOrganizations",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AcceptedMatchCount", table: "CharityOrganizations");
        migrationBuilder.DropColumn(name: "CancellationCount", table: "CharityOrganizations");
        migrationBuilder.DropColumn(name: "ReputationScore", table: "CharityOrganizations");
        migrationBuilder.DropColumn(name: "LastReceivedAtUtc", table: "CharityOrganizations");
    }
}
