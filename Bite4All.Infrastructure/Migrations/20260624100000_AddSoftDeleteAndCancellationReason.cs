using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bite4All.Infrastructure.Migrations;

/// <summary>
/// Adds:
/// - Driver.IsActive + Driver.SuspensionReason (soft suspend for drivers)
/// - Recipient.IsActive (soft delete for recipients)
/// - Vehicle.IsActive (soft deactivation for vehicles)
/// - PickupDocument.CancellationReason + PickupDocument.CancelledAtUtc
///   (structured cancellation audit trail)
/// </summary>
public partial class AddSoftDeleteAndCancellationReason : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Driver — suspend support
        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            table: "Drivers",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "SuspensionReason",
            table: "Drivers",
            type: "nvarchar(max)",
            nullable: true);

        // Recipient — soft delete
        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            table: "Recipients",
            type: "bit",
            nullable: false,
            defaultValue: true);

        // Vehicle — soft deactivation
        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            table: "Vehicles",
            type: "bit",
            nullable: false,
            defaultValue: true);

        // PickupDocument — structured cancellation
        migrationBuilder.AddColumn<string>(
            name: "CancellationReason",
            table: "PickupDocuments",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CancelledAtUtc",
            table: "PickupDocuments",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsActive", table: "Drivers");
        migrationBuilder.DropColumn(name: "SuspensionReason", table: "Drivers");
        migrationBuilder.DropColumn(name: "IsActive", table: "Recipients");
        migrationBuilder.DropColumn(name: "IsActive", table: "Vehicles");
        migrationBuilder.DropColumn(name: "CancellationReason", table: "PickupDocuments");
        migrationBuilder.DropColumn(name: "CancelledAtUtc", table: "PickupDocuments");
    }
}
