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
}
