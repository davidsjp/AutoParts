using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AutoParts.Api.Models;

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
