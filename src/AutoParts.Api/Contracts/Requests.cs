using System.ComponentModel.DataAnnotations;
using AutoParts.Api.Models;

namespace AutoParts.Api.Contracts;

public sealed record VehicleRequest(
    [Required, MaxLength(100)] string Manufacturer,
    [Required, MaxLength(100)] string Model,
    [MaxLength(100)] string? Chassis,
    [MaxLength(100)] string? Engine,
    [Range(1886, 2200)] int ModelYear,
    DateOnly? ProductionDate,
    [StringLength(17, MinimumLength = 17)] string? Vin,
    [MaxLength(7)] string? SerialNumber = null,
    [MaxLength(20)] string? Market = null,
    [MaxLength(20)] string? TypeCode = null,
    [Range(0, 9)] int? MarketRelevance = null,
    [MaxLength(500)] string? MarketRelevanceSource = null);

public sealed record VehicleChassisLookupResponse(
    string Query,
    IReadOnlyList<VehicleChassisMatchResponse> Matches);

public sealed record VehicleChassisMatchResponse(
    int VehicleId,
    string Manufacturer,
    string Model,
    string? Chassis,
    string? SerialNumber,
    string? Engine,
    int ModelYear,
    int MarketRelevance,
    int Relevance,
    string MatchType);

public sealed record PartRequest(
    [Required, MaxLength(100)] string OemPartNumber,
    [Required, MaxLength(500)] string Description,
    [Required, MaxLength(100)] string Category,
    [MaxLength(50)] string? Side,
    [MaxLength(50)] string? Position,
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

public sealed record PartCategoryResponse(string Category, int PartCount);

public sealed record PartCompatibilityLookupResponse(
    int PartId,
    string OemPartNumber,
    string Description,
    string Category,
    string? Side,
    string? Position,
    string Source,
    IReadOnlyList<CompatibleBrandResponse> Brands,
    IReadOnlyList<CompatibleVehicleResponse> Vehicles);

public sealed record CompatibleBrandResponse(
    string Manufacturer,
    IReadOnlyList<CompatibleModelResponse> Models);

public sealed record CompatibleModelResponse(
    string Model,
    int Relevance,
    CompatibilityStatus Status,
    int VehicleCount,
    IReadOnlyList<CompatibleVersionResponse> Versions);

public sealed record CompatibleVersionResponse(
    string? Chassis,
    string? Engine,
    string? TypeCode,
    string? Market,
    int? YearStart,
    int? YearEnd,
    int VehicleCount,
    int Relevance,
    CompatibilityStatus Status,
    int? Confidence);

public sealed record CompatibleVehicleResponse(
    int VehicleId,
    string Manufacturer,
    string Model,
    string? Chassis,
    string? Engine,
    string? TypeCode,
    string? Market,
    int? YearStart,
    int? YearEnd,
    string? Notes,
    int Relevance,
    CompatibilityStatus Status,
    int? Confidence,
    string Source,
    string? EvidenceText);

public sealed record CompatibilityRequest(
    [Range(1, int.MaxValue)] int VehicleId,
    DateOnly? ProductionStart,
    DateOnly? ProductionEnd,
    [MaxLength(1000)] string? Notes,
    [Range(0, 10)] int Relevance = 5,
    CompatibilityStatus Status = CompatibilityStatus.Suggested,
    [Range(0, 100)] int? Confidence = null,
    [MaxLength(100)] string? Source = null,
    [MaxLength(2000)] string? EvidenceText = null,
    [MaxLength(100)] string? ConfirmedByUserId = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProductionStart.HasValue && ProductionEnd.HasValue && ProductionEnd < ProductionStart)
            yield return new ValidationResult("ProductionEnd must be on or after ProductionStart.", [nameof(ProductionEnd)]);
    }
}

public sealed record CompatibilityUpdateRequest(
    DateOnly? ProductionStart,
    DateOnly? ProductionEnd,
    [MaxLength(1000)] string? Notes,
    [Range(0, 10)] int Relevance,
    CompatibilityStatus Status,
    [Range(0, 100)] int? Confidence,
    [MaxLength(100)] string? Source,
    [MaxLength(2000)] string? EvidenceText,
    [MaxLength(100)] string? ConfirmedByUserId) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProductionStart.HasValue && ProductionEnd.HasValue && ProductionEnd < ProductionStart)
            yield return new ValidationResult("ProductionEnd must be on or after ProductionStart.", [nameof(ProductionEnd)]);
    }
}
