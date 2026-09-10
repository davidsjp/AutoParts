using System.ComponentModel.DataAnnotations;

namespace AutoParts.Api.Models;

/// <summary>
/// A price observed in a marketplace listing for a specific OEM part.
/// Sample entries are kept explicit so they can never be mistaken for market data.
/// </summary>
public sealed class PartPriceObservation
{
    public int Id { get; set; }
    public int PartId { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(100)] public required string Source { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public bool IsSample { get; set; }
    public Part Part { get; set; } = null!;
}
