using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AutoParts.Api.Models;

public sealed class Vehicle
{
    public int Id { get; set; }
    [MaxLength(100)] public required string Manufacturer { get; set; }
    [MaxLength(100)] public required string Model { get; set; }
    [MaxLength(100)] public string? Chassis { get; set; }
    [MaxLength(100)] public string? Engine { get; set; }
    public int ModelYear { get; set; }
    public DateOnly? ProductionDate { get; set; }
    [MaxLength(17)] public string? Vin { get; set; }
    [MaxLength(7)] public string? SerialNumber { get; set; }
    [MaxLength(20)] public string? Market { get; set; }
    [MaxLength(20)] public string? TypeCode { get; set; }
    [JsonIgnore] public ICollection<PartCompatibility> Compatibilities { get; set; } = [];
}
