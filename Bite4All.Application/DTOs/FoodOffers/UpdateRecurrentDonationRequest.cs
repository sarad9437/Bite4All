using Bite4All.Domain.Enums;

namespace Bite4All.Application.DTOs.FoodOffers;

/// <summary>
/// Partial-update request za recurrent donation schedule.
/// Sva polja su opciona — menjaju se samo ona koja su prosleđena.
/// </summary>
public class UpdateRecurrentDonationRequest
{
    public decimal? ExpectedQuantityKg { get; set; }
    public FoodCategory? Category { get; set; }
    public TimeOnly? LocalCreationTime { get; set; }
    public TimeOnly? LocalPickupStart { get; set; }
    public TimeOnly? LocalPickupEnd { get; set; }
    public int? ShelfLifeHours { get; set; }
    /// <summary>
    /// Null znači "ne menjaj", prazan string znači "obriši napomenu".
    /// </summary>
    public string? NoteTemplate { get; set; }
}
