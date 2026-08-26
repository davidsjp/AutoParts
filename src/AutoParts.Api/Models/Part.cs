using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AutoParts.Api.Models;

/// <summary>
/// Commercial part record keyed by normalized OEM number. Description,
/// keyword group and applications are the fields exported to marketplace-ready
/// catalog views.
/// </summary>
public sealed class Part
{
    public int Id { get; set; }
    [MaxLength(100)] public required string OemPartNumber { get; set; }
    [MaxLength(500)] public required string Description { get; set; }
    [MaxLength(100)] public required string Category { get; set; }
    [MaxLength(50)] public string? Side { get; set; }
    [MaxLength(50)] public string? Position { get; set; }
    [MaxLength(100)] public string? SupersededByPartNumber { get; set; }
    [MaxLength(100)] public required string Source { get; set; }
    public DateOnly? CatalogDate { get; set; }
    public decimal? SuggestedValue { get; set; }
    [MaxLength(1000)] public required string KeywordGroup { get; set; }
    [MaxLength(1000)] public required string Applications { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    [JsonIgnore] public ICollection<PartCompatibility> Compatibilities { get; set; } = [];
}
