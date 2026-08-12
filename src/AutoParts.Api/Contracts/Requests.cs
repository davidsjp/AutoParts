using System.ComponentModel.DataAnnotations;

namespace AutoParts.Api.Contracts;

public sealed record VehicleRequest(
    [Required, MaxLength(100)] string Manufacturer,
    [Required, MaxLength(100)] string Model,
    [MaxLength(100)] string? Chassis,
    [MaxLength(100)] string? Engine,
    [Range(1886, 2200)] int ModelYear,
    DateOnly? ProductionDate,
    [StringLength(17, MinimumLength = 17)] string? Vin);

public sealed record PartRequest(
    [Required, MaxLength(100)] string OemPartNumber,
    [Required, MaxLength(500)] string Description,
    [Required, MaxLength(100)] string Category,
    [MaxLength(100)] string? SupersededByPartNumber,
    [Required, MaxLength(100)] string Source,
    DateOnly? CatalogDate = null,
    [Range(0, 999999999)] decimal? SuggestedValue = null,
    [MaxLength(1000)] string? KeywordGroup = null,
    [MaxLength(1000)] string? Applications = null);

public sealed record CatalogItemResponse(
    DateOnly? Data,
    string CodOem,
    string Titulo,
    decimal? ValorSugerido,
    string GrupoDePalavrasChaves,
    string Aplicacoes);

public sealed record CompatibilityRequest(
    [Range(1, int.MaxValue)] int VehicleId,
    DateOnly? ProductionStart,
    DateOnly? ProductionEnd,
    [MaxLength(1000)] string? Notes) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProductionStart.HasValue && ProductionEnd.HasValue && ProductionEnd < ProductionStart)
            yield return new ValidationResult("ProductionEnd must be on or after ProductionStart.", [nameof(ProductionEnd)]);
    }
}
