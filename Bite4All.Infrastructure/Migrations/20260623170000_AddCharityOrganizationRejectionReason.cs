using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bite4All.Infrastructure.Migrations;

/// <summary>
/// Adds RejectionReason column to CharityOrganizations table.
/// The domain entity already had this property but the migration was missing,
/// which would cause EF to fail on startup (pending model changes).
/// </summary>
public partial class AddCharityOrganizationRejectionReason : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RejectionReason",
            table: "CharityOrganizations",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RejectionReason",
            table: "CharityOrganizations");
    }
}
