namespace AutoParts.Api.Clients;

public interface IPartsCatalogClient
{
    Task<IReadOnlyList<CatalogSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<ExternalPart?> FindByOemNumberAsync(string oemPartNumber, CancellationToken cancellationToken = default);
    Task<ExternalCar?> GetCarAsync(int carId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExternalCarPart>> GetCarPartsAsync(int carId, CancellationToken cancellationToken = default);
}

public sealed record CatalogSearchResult(
    long Id,
    string PartNumber,
    string PartNumberClean,
    string Description,
    string? CategoryName,
    string? SubcategoryName,
    string? CarName);

public sealed record ExternalPart(
    string PartNumber,
    string PartNumberClean,
    string Description,
    string? AdditionalInfo,
    decimal? Weight,
    IReadOnlyList<ExternalVehicle> Vehicles);

public sealed record ExternalVehicle(
    int CarId,
    string CarName,
    string CarSlug,
    string? Chassis,
    string? Engine,
    string? BodyType,
    int? YearStart,
    int? YearEnd,
    string? CategoryName,
    string? SubcategoryName);

public sealed record ExternalCar(
    int Id,
    string DisplayName,
    string? Chassis,
    string? Engine,
    string? BodyType,
    string? TypeCode,
    int TotalParts,
    string ScrapeStatus,
    string? ImageUrl = null);

public sealed record ExternalCarPart(
    long Id,
    string PartNumber,
    string PartNumberClean,
    string Description,
    string? AdditionalInfo,
    string? PartDate,
    string? Quantity,
    string? Notes,
    string? CategoryName,
    string? SubcategoryName);

public sealed record ExternalCarPartsPage(
    IReadOnlyList<ExternalCarPart> Parts,
    int Total,
    int Limit,
    int Offset);
