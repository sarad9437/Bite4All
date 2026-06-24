using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bite4All.Infrastructure.Migrations;

/// <summary>
/// Adds missing foreign key indexes on PickupDocuments that Entity Framework
/// infers from navigation properties but that were never explicitly created in
/// any previous migration. Without these indexes, queries that filter by
/// FoodOfferId, HospitalityPartnerId, CharityOrganizationId, DriverId or
/// VehicleId perform full table scans as the data grows.
/// </summary>
public partial class AddPickupDocumentFkIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_PickupDocuments_FoodOfferId",
            table: "PickupDocuments",
            column: "FoodOfferId");

        migrationBuilder.CreateIndex(
            name: "IX_PickupDocuments_HospitalityPartnerId",
            table: "PickupDocuments",
            column: "HospitalityPartnerId");

        migrationBuilder.CreateIndex(
            name: "IX_PickupDocuments_CharityOrganizationId",
            table: "PickupDocuments",
            column: "CharityOrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_PickupDocuments_DriverId",
            table: "PickupDocuments",
            column: "DriverId");

        migrationBuilder.CreateIndex(
            name: "IX_PickupDocuments_VehicleId",
            table: "PickupDocuments",
            column: "VehicleId");

        migrationBuilder.CreateIndex(
            name: "IX_PickupDocuments_Status",
            table: "PickupDocuments",
            column: "Status");

        // OfferMatches — FoodOfferId + CharityOrganizationId are also commonly
        // filtered together; a composite covering index speeds up matching queries.
        migrationBuilder.CreateIndex(
            name: "IX_OfferMatches_FoodOfferId_CharityOrganizationId",
            table: "OfferMatches",
            columns: new[] { "FoodOfferId", "CharityOrganizationId" });

        migrationBuilder.CreateIndex(
            name: "IX_OfferMatches_CharityOrganizationId",
            table: "OfferMatches",
            column: "CharityOrganizationId");

        // Notifications are queried by (RecipientType, RecipientId) very frequently.
        migrationBuilder.CreateIndex(
            name: "IX_Notifications_RecipientType_RecipientId",
            table: "Notifications",
            columns: new[] { "RecipientType", "RecipientId" });

        // ReputationEntries are filtered by (RatedActorType, RatedActorId).
        migrationBuilder.CreateIndex(
            name: "IX_ReputationEntries_RatedActorType_RatedActorId",
            table: "ReputationEntries",
            columns: new[] { "RatedActorType", "RatedActorId" });

        // RecipientMealDistributions — already has a composite index on
        // (PickupDocumentId, RecipientId) from an earlier migration.
        // Add CharityOrganizationId standalone for the org-scoped queries.
        // (IX_RecipientMealDistributions_CharityOrganizationId already exists per
        //  the AddRecipientMealDistributions migration — skip it here.)

        // FoodOffers — filter by Status is common in scheduler and search.
        migrationBuilder.CreateIndex(
            name: "IX_FoodOffers_Status",
            table: "FoodOffers",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_FoodOffers_HospitalityPartnerId",
            table: "FoodOffers",
            column: "HospitalityPartnerId");

        migrationBuilder.CreateIndex(
            name: "IX_FoodOffers_ExpiresAtUtc",
            table: "FoodOffers",
            column: "ExpiresAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_PickupDocuments_FoodOfferId", "PickupDocuments");
        migrationBuilder.DropIndex("IX_PickupDocuments_HospitalityPartnerId", "PickupDocuments");
        migrationBuilder.DropIndex("IX_PickupDocuments_CharityOrganizationId", "PickupDocuments");
        migrationBuilder.DropIndex("IX_PickupDocuments_DriverId", "PickupDocuments");
        migrationBuilder.DropIndex("IX_PickupDocuments_VehicleId", "PickupDocuments");
        migrationBuilder.DropIndex("IX_PickupDocuments_Status", "PickupDocuments");
        migrationBuilder.DropIndex("IX_OfferMatches_FoodOfferId_CharityOrganizationId", "OfferMatches");
        migrationBuilder.DropIndex("IX_OfferMatches_CharityOrganizationId", "OfferMatches");
        migrationBuilder.DropIndex("IX_Notifications_RecipientType_RecipientId", "Notifications");
        migrationBuilder.DropIndex("IX_ReputationEntries_RatedActorType_RatedActorId", "ReputationEntries");
        migrationBuilder.DropIndex("IX_FoodOffers_Status", "FoodOffers");
        migrationBuilder.DropIndex("IX_FoodOffers_HospitalityPartnerId", "FoodOffers");
        migrationBuilder.DropIndex("IX_FoodOffers_ExpiresAtUtc", "FoodOffers");
    }
}
