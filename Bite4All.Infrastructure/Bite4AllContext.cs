using Bite4All.Domain.Common;
using Bite4All.Domain.Entities;
using Bite4All.Domain.Enums;
using Bite4All.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bite4All.Infrastructure;

public class Bite4AllContext(DbContextOptions<Bite4AllContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<City> Cities => Set<City>();
    public DbSet<HospitalityPartner> HospitalityPartners => Set<HospitalityPartner>();
    public DbSet<CharityOrganization> CharityOrganizations => Set<CharityOrganization>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Recipient> Recipients => Set<Recipient>();
    public DbSet<FoodOffer> FoodOffers => Set<FoodOffer>();
    public DbSet<FoodOfferItem> FoodOfferItems => Set<FoodOfferItem>();
    public DbSet<RecurrentDonation> RecurrentDonations => Set<RecurrentDonation>();
    public DbSet<OfferMatch> OfferMatches => Set<OfferMatch>();
    public DbSet<PickupDocument> PickupDocuments => Set<PickupDocument>();
    public DbSet<PickupIssue> PickupIssues => Set<PickupIssue>();
    public DbSet<ReputationEntry> ReputationEntries => Set<ReputationEntry>();
    public DbSet<ReputationSnapshot> ReputationSnapshots => Set<ReputationSnapshot>();
    public DbSet<BadgeAssignment> BadgeAssignments => Set<BadgeAssignment>();
    public DbSet<SpecialCampaign> SpecialCampaigns => Set<SpecialCampaign>();
    public DbSet<PlatformMessage> PlatformMessages => Set<PlatformMessage>();
    public DbSet<BlockRelation> BlockRelations => Set<BlockRelation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RecipientMealDistribution> RecipientMealDistributions => Set<RecipientMealDistribution>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Entity>().Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<City>().HasData(
            new City { Id = 1, Name = "Beograd", IsActive = true },
            new City { Id = 2, Name = "Novi Sad", IsActive = true },
            new City { Id = 3, Name = "Nis", IsActive = true });

        builder.Entity<HospitalityPartner>().Property(p => p.TotalDonatedKg).HasPrecision(10, 2);
        builder.Entity<HospitalityPartner>().Property(p => p.Latitude).HasPrecision(9, 6);
        builder.Entity<HospitalityPartner>().Property(p => p.Longitude).HasPrecision(9, 6);
        builder.Entity<HospitalityPartner>().HasIndex(p => p.TaxIdentificationNumber).IsUnique();

        builder.Entity<Driver>().Property(d => d.CurrentLatitude).HasPrecision(9, 6);
        builder.Entity<Driver>().Property(d => d.CurrentLongitude).HasPrecision(9, 6);

        builder.Entity<CharityOrganization>().Property(o => o.DailyCapacityKg).HasPrecision(10, 2);
        builder.Entity<CharityOrganization>().Property(o => o.TemporaryExtraCapacityKg).HasPrecision(10, 2);
        builder.Entity<CharityOrganization>().Property(o => o.MatchCompensationBonus).HasPrecision(10, 2);
        builder.Entity<CharityOrganization>().Property(o => o.Latitude).HasPrecision(9, 6);
        builder.Entity<CharityOrganization>().Property(o => o.Longitude).HasPrecision(9, 6);
        builder.Entity<CharityOrganization>().HasIndex(o => o.TaxIdentificationNumber).IsUnique();

        builder.Entity<Vehicle>().Property(v => v.CapacityKg).HasPrecision(10, 2);
        builder.Entity<FoodOffer>().Property(o => o.TotalQuantityKg).HasPrecision(10, 2);
        builder.Entity<FoodOfferItem>().Property(i => i.Quantity).HasPrecision(10, 2);
        builder.Entity<OfferMatch>().Property(m => m.Score).HasPrecision(10, 2);
        builder.Entity<PickupDocument>().Property(p => p.PlannedQuantityKg).HasPrecision(10, 2);
        builder.Entity<PickupDocument>().Property(p => p.ActualQuantityKg).HasPrecision(10, 2);
        builder.Entity<SpecialCampaign>().Property(c => c.TargetQuantityKg).HasPrecision(10, 2);
        builder.Entity<SpecialCampaign>().Property(c => c.CurrentQuantityKg).HasPrecision(10, 2);
        builder.Entity<RecurrentDonation>().Property(r => r.ExpectedQuantityKg).HasPrecision(10, 2);
        builder.Entity<ReputationSnapshot>().HasIndex(r => new { r.ActorType, r.ActorId, r.CreatedAtUtc });

        builder.Entity<FoodOffer>()
            .HasMany(o => o.Items)
            .WithOne(i => i.FoodOffer)
            .HasForeignKey(i => i.FoodOfferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<FoodOffer>()
            .HasMany(o => o.Matches)
            .WithOne(m => m.FoodOffer)
            .HasForeignKey(m => m.FoodOfferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<BlockRelation>()
            .HasIndex(b => new { b.HospitalityPartnerId, b.CharityOrganizationId })
            .IsUnique();

        builder.Entity<PickupDocument>()
            .HasIndex(p => p.DocumentNumber)
            .IsUnique();

        builder.Entity<Recipient>()
            .HasIndex(r => new { r.CharityOrganizationId, r.InternalCode })
            .IsUnique();

        builder.Entity<RecipientMealDistribution>()
            .HasIndex(d => new { d.PickupDocumentId, d.RecipientId });

        builder.Entity<IdempotencyRecord>().Property(r => r.RouteKey).HasMaxLength(450);
        builder.Entity<IdempotencyRecord>().Property(r => r.RequestHash).HasMaxLength(128);
        builder.Entity<IdempotencyRecord>().Property(r => r.ResponseContentType).HasMaxLength(256);
        builder.Entity<IdempotencyRecord>().HasIndex(r => r.RouteKey).IsUnique();
        builder.Entity<IdempotencyRecord>().HasIndex(r => r.ExpiresAtUtc);

        SeedDemoData(builder);
    }

    private static void SeedDemoData(ModelBuilder builder)
    {
        builder.Entity<HospitalityPartner>().HasData(new HospitalityPartner
        {
            Id = 1,
            Name = "Pekara Sunce",
            PartnerType = "Pekara",
            Address = "Bulevar kralja Aleksandra 100",
            CityId = 1,
            ContactEmail = "pekara@bite4all.local",
            ContactPhone = "+381111111",
            TaxIdentificationNumber = "100000001",
            ApprovalStatus = ApprovalStatus.Approved,
            Latitude = 44.805m,
            Longitude = 20.475m,
            ReputationScore = 4.7
        });

        builder.Entity<CharityOrganization>().HasData(new CharityOrganization
        {
            Id = 1,
            Name = "Narodna kuhinja Centar",
            Address = "Nemanjina 1",
            CityId = 1,
            ContactEmail = "kuhinja@bite4all.local",
            ContactPhone = "+381222222",
            TaxIdentificationNumber = "200000001",
            ApprovalStatus = ApprovalStatus.Approved,
            Latitude = 44.807m,
            Longitude = 20.463m,
            DailyCapacityKg = 120,
            ReputationScore = 4.6
        });

        builder.Entity<Driver>().HasData(new Driver
        {
            Id = 1,
            CharityOrganizationId = 1,
            FullName = "Marko Markovic",
            Email = "vozac@bite4all.local",
            Phone = "+381333333",
            IsAvailable = true
        });

        builder.Entity<Vehicle>().HasData(new Vehicle
        {
            Id = 1,
            CharityOrganizationId = 1,
            RegistrationNumber = "BG-001-AA",
            CapacityKg = 150,
            HasCooling = false,
            IsAvailable = true
        });

        builder.Entity<Recipient>().HasData(new Recipient
        {
            Id = 1,
            CharityOrganizationId = 1,
            InternalCode = "NKC-001",
            DietaryRestrictions = DietaryTag.None
        });
    }
}
