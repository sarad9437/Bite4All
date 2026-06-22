namespace Bite4All.Domain.Enums;

public enum FoodOfferStatus
{
    Draft = 1,
    PendingRestaurantConfirmation = 2,
    Active = 3,
    Reserved = 4,
    PublicFallback = 5,
    Completed = 6,
    Cancelled = 7,
    Expired = 8
}
