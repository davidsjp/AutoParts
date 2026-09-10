using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AutoParts.Api.Models;

/// <summary>Immutable audit entry for a human or automated compatibility decision.</summary>
public sealed class CompatibilityReviewLog
{
    public int Id { get; set; }
    public int PartCompatibilityId { get; set; }
    [JsonIgnore] public PartCompatibility PartCompatibility { get; set; } = null!;
    [Required, MaxLength(50)] public string Action { get; set; } = null!;
    public CompatibilityStatus? PreviousStatus { get; set; }
    public CompatibilityStatus NewStatus { get; set; }
    public int? PreviousRelevance { get; set; }
    public int NewRelevance { get; set; }
    [MaxLength(100)] public string? PerformedByUserId { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTimeOffset PerformedAt { get; set; }
}
