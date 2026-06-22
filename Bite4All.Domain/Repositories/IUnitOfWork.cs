using Bite4All.Domain.Entities;

namespace Bite4All.Domain.Repositories;

public interface IUnitOfWork
{
    IRepository<City> Cities { get; }
    IRepository<HospitalityPartner> HospitalityPartners { get; }
    IRepository<CharityOrganization> CharityOrganizations { get; }
    IRepository<Driver> Drivers { get; }
    IRepository<Vehicle> Vehicles { get; }
    IRepository<Recipient> Recipients { get; }
    IRepository<FoodOffer> FoodOffers { get; }
    IRepository<FoodOfferItem> FoodOfferItems { get; }
    IRepository<RecurrentDonation> RecurrentDonations { get; }
    IRepository<OfferMatch> OfferMatches { get; }
    IRepository<PickupDocument> PickupDocuments { get; }
    IRepository<PickupIssue> PickupIssues { get; }
    IRepository<ReputationEntry> ReputationEntries { get; }
    IRepository<ReputationSnapshot> ReputationSnapshots { get; }
    IRepository<BadgeAssignment> BadgeAssignments { get; }
    IRepository<SpecialCampaign> SpecialCampaigns { get; }
    IRepository<PlatformMessage> PlatformMessages { get; }
    IRepository<BlockRelation> BlockRelations { get; }
    IRepository<Notification> Notifications { get; }
    IRepository<RecipientMealDistribution> RecipientMealDistributions { get; }
    IRepository<IdempotencyRecord> IdempotencyRecords { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
