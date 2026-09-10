using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AutoParts.Api.Models;

/// <summary>
/// Join entity that states a part applies to a vehicle for an optional
/// production interval. Notes keep source-specific hints such as category,
/// subcategory, serial number or type code.
/// </summary>
public sealed class PartCompatibility
{
    public int Id { get; set; }
    public int PartId { get; set; }
    [JsonIgnore] public Part Part { get; set; } = null!;
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public DateOnly? ProductionStart { get; set; }
    public DateOnly? ProductionEnd { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    [Range(1, 10)] public int Relevance { get; set; } = 5;
    public CompatibilityStatus Status { get; set; } = CompatibilityStatus.Suggested;
    [Range(0, 100)] public int? Confidence { get; set; }
    [Required, MaxLength(100)] public string Source { get; set; } = "Manual";
    [MaxLength(2000)] public string? EvidenceText { get; set; }
    [MaxLength(100)] public string? ConfirmedByUserId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    [JsonIgnore] public ICollection<CompatibilityReviewLog> ReviewLogs { get; set; } = [];
}
