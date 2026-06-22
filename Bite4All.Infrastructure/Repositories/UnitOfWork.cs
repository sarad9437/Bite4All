using Bite4All.Domain.Entities;
using Bite4All.Domain.Repositories;

namespace Bite4All.Infrastructure.Repositories;

public class UnitOfWork(Bite4AllContext context) : IUnitOfWork
{
    public IRepository<City> Cities { get; } = new Repository<City>(context);
    public IRepository<HospitalityPartner> HospitalityPartners { get; } = new Repository<HospitalityPartner>(context);
    public IRepository<CharityOrganization> CharityOrganizations { get; } = new Repository<CharityOrganization>(context);
    public IRepository<Driver> Drivers { get; } = new Repository<Driver>(context);
    public IRepository<Vehicle> Vehicles { get; } = new Repository<Vehicle>(context);
    public IRepository<Recipient> Recipients { get; } = new Repository<Recipient>(context);
    public IRepository<FoodOffer> FoodOffers { get; } = new Repository<FoodOffer>(context);
    public IRepository<FoodOfferItem> FoodOfferItems { get; } = new Repository<FoodOfferItem>(context);
    public IRepository<RecurrentDonation> RecurrentDonations { get; } = new Repository<RecurrentDonation>(context);
    public IRepository<OfferMatch> OfferMatches { get; } = new Repository<OfferMatch>(context);
    public IRepository<PickupDocument> PickupDocuments { get; } = new Repository<PickupDocument>(context);
    public IRepository<PickupIssue> PickupIssues { get; } = new Repository<PickupIssue>(context);
    public IRepository<ReputationEntry> ReputationEntries { get; } = new Repository<ReputationEntry>(context);
    public IRepository<ReputationSnapshot> ReputationSnapshots { get; } = new Repository<ReputationSnapshot>(context);
    public IRepository<BadgeAssignment> BadgeAssignments { get; } = new Repository<BadgeAssignment>(context);
    public IRepository<SpecialCampaign> SpecialCampaigns { get; } = new Repository<SpecialCampaign>(context);
    public IRepository<PlatformMessage> PlatformMessages { get; } = new Repository<PlatformMessage>(context);
    public IRepository<BlockRelation> BlockRelations { get; } = new Repository<BlockRelation>(context);
    public IRepository<Notification> Notifications { get; } = new Repository<Notification>(context);
    public IRepository<RecipientMealDistribution> RecipientMealDistributions { get; } = new Repository<RecipientMealDistribution>(context);
    public IRepository<IdempotencyRecord> IdempotencyRecords { get; } = new Repository<IdempotencyRecord>(context);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
