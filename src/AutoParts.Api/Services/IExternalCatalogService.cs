using AutoParts.Api.Clients;

namespace AutoParts.Api.Services;

public interface IExternalCatalogService
{
    Task<IReadOnlyList<CatalogSearchResult>> SearchAsync(string query, CancellationToken ct);
    Task<PartImportPreview> PreviewAsync(string oemPartNumber, CancellationToken ct);
    Task<ImportPartResult> ImportByOemAsync(string oemPartNumber, CancellationToken ct);
    Task<BulkVehicleImportResult> ImportVehicleCatalogAsync(int externalCarId, BulkVehicleImportRequest request, CancellationToken ct);
}

public sealed record PartImportPreview(
    string OemPartNumber,
    string OriginalDescription,
    string TranslatedTitle,
    string Category,
    string KeywordGroup,
    string Applications,
    int VehicleCount,
    string Source);

public sealed record ImportPartResult(
    int PartId,
    string OemPartNumber,
    string Description,
    bool Created,
    int VehiclesImported,
    int CompatibilitiesCreated,
    int ImportRunId);

public sealed record BulkVehicleImportRequest(
    string SerialNumber,
    string Model,
    string Chassis,
    string Engine,
    int ModelYear,
    DateOnly ProductionDate,
    DateOnly CompatibilityStart,
    DateOnly CompatibilityEnd,
    string Market,
    string TypeCode);

public sealed record BulkVehicleImportResult(
    int VehicleId,
    string SerialNumber,
    string Vehicle,
    int SourceRows,
    int ValidUniqueParts,
    int PartsCreated,
    int ExistingPartsReused,
    int CompatibilitiesCreated,
    int ImportRunId);
