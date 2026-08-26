using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AutoParts.Api.Models;

/// <summary>
/// Local vehicle record used to group compatible parts. BMW catalog metadata
/// such as serial number, market and type code is optional but important for
/// imported BMV.parts/RealOEM workflows.
/// </summary>
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
