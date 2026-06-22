using Bite4All.Domain.Common;
using Bite4All.Domain.Enums;

namespace Bite4All.Domain.Entities;

public class OfferMatch : Entity
{
    public int FoodOfferId { get; set; }
    public FoodOffer? FoodOffer { get; set; }
    public int CharityOrganizationId { get; set; }
    public CharityOrganization? CharityOrganization { get; set; }
    public decimal Score { get; set; }
    public int Rank { get; set; }
    public MatchDecision Decision { get; set; } = MatchDecision.Pending;
    public DateTime? NotifiedAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}
