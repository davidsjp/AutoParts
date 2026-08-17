using AutoParts.Api.Clients;
using AutoParts.Api.Data;
using AutoParts.Api.Infrastructure;
using AutoParts.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Services;

public sealed class ExternalCatalogService(
    IPartsCatalogClient client,
    IPartTranslationService translator,
    AutoPartsDbContext db) : IExternalCatalogService
{
    public Task<IReadOnlyList<CatalogSearchResult>> SearchAsync(string query, CancellationToken ct) =>
        client.SearchAsync(query, ct);

    public async Task<PartImportPreview> PreviewAsync(string oemPartNumber, CancellationToken ct)
    {
        var external = await GetExternalPartAsync(oemPartNumber, ct);
        var translated = await translator.TranslateAsync(external, ct);
        return new PartImportPreview(external.PartNumberClean, external.Description,
            translated.Title, translated.Category, translated.KeywordGroup,
            translated.Applications, external.Vehicles.DistinctBy(x => x.CarId).Count(), "BMV.parts");
    }

    public async Task<ImportPartResult> ImportByOemAsync(string oemPartNumber, CancellationToken ct)
    {
        var run = new ImportRun
        {
            Source = "BMV.parts",
            StartedAt = DateTimeOffset.UtcNow,
            Status = "Running",
            RecordsProcessed = 0
        };
        db.ImportRuns.Add(run);
        await db.SaveChangesAsync(ct);

        try
        {
            var external = await GetExternalPartAsync(oemPartNumber, ct);
            var translated = await translator.TranslateAsync(external, ct);
            var normalizedOem = NormalizeOem(external.PartNumberClean);
            var part = await db.Parts.FirstOrDefaultAsync(x => x.OemPartNumber == normalizedOem, ct);
            var created = part is null;

            if (part is null)
            {
                var now = DateTimeOffset.UtcNow;
                part = new Part
                {
                    OemPartNumber = normalizedOem,
                    Description = translated.Title,
                    Category = translated.Category,
                    Source = "BMV.parts",
                    KeywordGroup = translated.KeywordGroup,
                    Applications = translated.Applications,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Parts.Add(part);
                await db.SaveChangesAsync(ct);
            }
            else if (part.Source.Equals("BMV.parts", StringComparison.OrdinalIgnoreCase))
            {
                part.Description = translated.Title;
                part.Category = translated.Category;
                part.KeywordGroup = translated.KeywordGroup;
                part.Applications = translated.Applications;
                part.UpdatedAt = DateTimeOffset.UtcNow;
            }

            var vehiclesImported = 0;
            var compatibilitiesCreated = 0;
            foreach (var sourceVehicle in external.Vehicles.DistinctBy(x => x.CarId))
            {
                var model = sourceVehicle.CarName.Trim();
                var chassis = sourceVehicle.Chassis?.Trim();
                var engine = sourceVehicle.Engine?.Trim();
                var vehicle = await db.Vehicles.FirstOrDefaultAsync(x =>
                    x.Manufacturer == "BMW" && x.Model == model && x.Chassis == chassis && x.Engine == engine, ct);
                if (vehicle is null)
                {
                    vehicle = new Vehicle
                    {
                        Manufacturer = "BMW",
                        Model = model,
                        Chassis = chassis,
                        Engine = engine,
                        ModelYear = ValidYear(sourceVehicle.YearStart) ?? 1886,
                        ProductionDate = ToDate(sourceVehicle.YearStart)
                    };
                    db.Vehicles.Add(vehicle);
                    await db.SaveChangesAsync(ct);
                    vehiclesImported++;
                }

                var productionStart = ToDate(sourceVehicle.YearStart);
                var exists = await db.PartCompatibilities.AnyAsync(x =>
                    x.PartId == part.Id && x.VehicleId == vehicle.Id && x.ProductionStart == productionStart, ct);
                if (!exists)
                {
                    db.PartCompatibilities.Add(new PartCompatibility
                    {
                        PartId = part.Id,
                        VehicleId = vehicle.Id,
                        ProductionStart = productionStart,
                        ProductionEnd = ToEndDate(sourceVehicle.YearEnd),
                        Notes = JoinNotes(sourceVehicle)
                    });
                    compatibilitiesCreated++;
                }
            }

            run.Status = "Completed";
            run.FinishedAt = DateTimeOffset.UtcNow;
            run.RecordsProcessed = 1;
            await db.SaveChangesAsync(ct);
            return new ImportPartResult(part.Id, part.OemPartNumber, part.Description, created,
                vehiclesImported, compatibilitiesCreated, run.Id);
        }
        catch (Exception ex)
        {
            run.Status = "Failed";
            run.FinishedAt = DateTimeOffset.UtcNow;
            run.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<BulkVehicleImportResult> ImportVehicleCatalogAsync(
        int externalCarId,
        BulkVehicleImportRequest request,
        CancellationToken ct)
    {
        if (request.CompatibilityEnd.HasValue && request.CompatibilityEnd < request.CompatibilityStart)
            throw new ApiException(400, "CompatibilityEnd must be on or after CompatibilityStart.");

        var run = new ImportRun
        {
            Source = $"BMV.parts car {externalCarId}",
            StartedAt = DateTimeOffset.UtcNow,
            Status = "Running",
            RecordsProcessed = 0
        };
        db.ImportRuns.Add(run);
        await db.SaveChangesAsync(ct);

        try
        {
            var externalCar = await client.GetCarAsync(externalCarId, ct)
                ?? throw new ApiException(404, "Vehicle not found in BMV.parts.");
            var sourceRows = await client.GetCarPartsAsync(externalCarId, ct);
            var validParts = sourceRows
                .Where(x => !string.IsNullOrWhiteSpace(x.PartNumberClean)
                    && NormalizeOem(x.PartNumberClean).Any(char.IsDigit)
                    && NormalizeOem(x.PartNumberClean).Any(character => character != '0')
                    && !string.IsNullOrWhiteSpace(x.Description))
                .GroupBy(x => NormalizeOem(x.PartNumberClean), StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();
            if (validParts.Count == 0)
                throw new ApiException(422, "BMV.parts returned no valid parts for this vehicle.");

            var serial = request.SerialNumber.Trim().ToUpperInvariant();
            var vehicle = await db.Vehicles.FirstOrDefaultAsync(x => x.SerialNumber == serial, ct);
            if (vehicle is null)
            {
                vehicle = new Vehicle
                {
                    Manufacturer = "BMW",
                    Model = request.Model.Trim(),
                    Chassis = request.Chassis.Trim(),
                    Engine = request.Engine.Trim(),
                    ModelYear = request.ModelYear,
                    ProductionDate = request.ProductionDate,
                    SerialNumber = serial,
                    Market = request.Market.Trim().ToUpperInvariant(),
                    TypeCode = request.TypeCode.Trim().ToUpperInvariant()
                };
                db.Vehicles.Add(vehicle);
                await db.SaveChangesAsync(ct);
            }

            var existingParts = await db.Parts.ToDictionaryAsync(x => x.OemPartNumber, StringComparer.OrdinalIgnoreCase, ct);
            var newParts = new List<Part>();
            var now = DateTimeOffset.UtcNow;
            var application = request.CompatibilityEnd.HasValue
                ? $"BMW {request.Chassis} {request.Model} {request.CompatibilityStart.Year} a {request.CompatibilityEnd.Value.Year}"
                : $"BMW {request.Chassis} {request.Model} a partir de {request.CompatibilityStart.Year} (fim de produção a confirmar)";
            foreach (var sourcePart in validParts)
            {
                var oem = NormalizeOem(sourcePart.PartNumberClean);
                if (existingParts.ContainsKey(oem)) continue;
                var part = new Part
                {
                    OemPartNumber = oem,
                    Description = sourcePart.Description.Trim(),
                    Category = sourcePart.CategoryName?.Trim() ?? "BMV.parts",
                    Source = "BMV.parts",
                    KeywordGroup = $"{sourcePart.Description} {oem}".ToLowerInvariant(),
                    Applications = application,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                newParts.Add(part);
                existingParts[oem] = part;
            }
            db.Parts.AddRange(newParts);
            await db.SaveChangesAsync(ct);

            var existingCompatibilityPartIds = await db.PartCompatibilities
                .Where(x => x.VehicleId == vehicle.Id)
                .Select(x => x.PartId)
                .ToHashSetAsync(ct);
            var compatibilities = new List<PartCompatibility>();
            foreach (var sourcePart in validParts)
            {
                var part = existingParts[NormalizeOem(sourcePart.PartNumberClean)];
                if (existingCompatibilityPartIds.Contains(part.Id)) continue;
                compatibilities.Add(new PartCompatibility
                {
                    PartId = part.Id,
                    VehicleId = vehicle.Id,
                    ProductionStart = request.CompatibilityStart,
                    ProductionEnd = request.CompatibilityEnd,
                    Notes = $"Serial {serial}; Type Code {request.TypeCode}; BMV.parts car {externalCar.Id}"
                });
            }
            db.PartCompatibilities.AddRange(compatibilities);
            run.Status = "Completed";
            run.FinishedAt = DateTimeOffset.UtcNow;
            run.RecordsProcessed = validParts.Count;
            await db.SaveChangesAsync(ct);

            return new BulkVehicleImportResult(vehicle.Id, serial,
                $"{request.Chassis} {request.Model}", sourceRows.Count, validParts.Count,
                newParts.Count, validParts.Count - newParts.Count, compatibilities.Count, run.Id);
        }
        catch (Exception ex)
        {
            run.Status = "Failed";
            run.FinishedAt = DateTimeOffset.UtcNow;
            run.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private static string NormalizeOem(string value) => new(value.Where(char.IsLetterOrDigit).ToArray());
    private static int? ValidYear(int? year) => year is >= 1886 and <= 2200 ? year : null;
    private static DateOnly? ToDate(int? year) => ValidYear(year) is int value ? new DateOnly(value, 1, 1) : null;
    private static DateOnly? ToEndDate(int? year) => ValidYear(year) is int value ? new DateOnly(value, 12, 31) : null;
    private async Task<ExternalPart> GetExternalPartAsync(string oemPartNumber, CancellationToken ct) =>
        await client.FindByOemNumberAsync(oemPartNumber, ct)
        ?? throw new ApiException(404, "Part not found in BMV.parts.");
    private static string JoinNotes(ExternalVehicle vehicle) =>
        string.Join(" / ", new[] { vehicle.CategoryName, vehicle.SubcategoryName, vehicle.BodyType }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
