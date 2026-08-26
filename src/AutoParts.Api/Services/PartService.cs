using AutoParts.Api.Contracts;
using AutoParts.Api.Data;
using AutoParts.Api.Infrastructure;
using AutoParts.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Services;

public sealed class PartService(AutoPartsDbContext db, IPartMetadataService metadataService) : IPartService
{
    public async Task<IReadOnlyList<Part>> GetAllAsync(CancellationToken ct) => await db.Parts.AsNoTracking().OrderBy(x => x.OemPartNumber).ToListAsync(ct);
    public async Task<IReadOnlyList<CatalogItemResponse>> GetCatalogAsync(CancellationToken ct) =>
        await db.Parts.AsNoTracking().OrderBy(x => x.Id).Select(x => new CatalogItemResponse(
            x.CatalogDate, x.OemPartNumber, x.Description, x.SuggestedValue,
            x.KeywordGroup, x.Applications)).ToListAsync(ct);
    public async Task<Part> GetAsync(int id, CancellationToken ct) => await db.Parts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound();
    public async Task<Part> GetByOemAsync(string oem, CancellationToken ct)
    {
        var normalized = NormalizeOem(oem);
        return await db.Parts.AsNoTracking().FirstOrDefaultAsync(x => x.OemPartNumber == normalized, ct) ?? throw NotFound();
    }
    public async Task<PartCompatibilityLookupResponse> GetCompatibilityLookupByOemAsync(string oem, CancellationToken ct)
    {
        var normalized = NormalizeOem(oem);
        var part = await db.Parts.AsNoTracking()
            .Include(x => x.Compatibilities)
            .ThenInclude(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.OemPartNumber == normalized, ct) ?? throw NotFound();

        var vehicles = part.Compatibilities
            .Select(compatibility =>
            {
                var vehicle = compatibility.Vehicle;
                return new CompatibleVehicleResponse(
                    vehicle.Id,
                    vehicle.Manufacturer,
                    vehicle.Model,
                    vehicle.Chassis,
                    vehicle.Engine,
                    vehicle.TypeCode,
                    vehicle.Market,
                    compatibility.ProductionStart?.Year ?? vehicle.ProductionDate?.Year ?? vehicle.ModelYear,
                    compatibility.ProductionEnd?.Year,
                    compatibility.Notes);
            })
            .OrderBy(x => x.Manufacturer)
            .ThenBy(x => x.Model)
            .ThenBy(x => x.Chassis)
            .ThenBy(x => x.Engine)
            .ThenBy(x => x.YearStart)
            .ToList();

        var brands = vehicles
            .GroupBy(x => x.Manufacturer)
            .Select(brand => new CompatibleBrandResponse(
                brand.Key,
                brand.GroupBy(x => x.Model)
                    .Select(model => new CompatibleModelResponse(
                        model.Key,
                        model.GroupBy(x => new { x.Chassis, x.Engine, x.TypeCode, x.Market })
                            .Select(version => new CompatibleVersionResponse(
                                version.Key.Chassis,
                                version.Key.Engine,
                                version.Key.TypeCode,
                                version.Key.Market,
                                version.Min(x => x.YearStart),
                                version.Max(x => x.YearEnd ?? x.YearStart),
                                version.Select(x => x.VehicleId).Distinct().Count()))
                            .OrderBy(x => x.Chassis)
                            .ThenBy(x => x.Engine)
                            .ThenBy(x => x.YearStart)
                            .ToList()))
                    .OrderBy(x => x.Model)
                    .ToList()))
            .OrderBy(x => x.Manufacturer)
            .ToList();

        return new PartCompatibilityLookupResponse(
            part.Id,
            part.OemPartNumber,
            part.Description,
            part.Category,
            part.Side,
            part.Position,
            part.Source,
            brands,
            vehicles);
    }
    public async Task<Part> CreateAsync(PartRequest r, CancellationToken ct)
    {
        var oem = NormalizeOem(r.OemPartNumber);
        if (await db.Parts.AnyAsync(x => x.OemPartNumber == oem, ct)) throw new ApiException(409, "OEM part number already exists.");
        var now = DateTimeOffset.UtcNow;
        var description = r.Description.Trim();
        var category = r.Category.Trim();
        var metadata = metadataService.Extract(description, category);
        var entity = new Part { OemPartNumber = oem, Description = description, Category = category, Side = NormalizeText(r.Side) ?? metadata.Side, Position = NormalizeText(r.Position) ?? metadata.Position, SupersededByPartNumber = NormalizeOptional(r.SupersededByPartNumber), Source = r.Source.Trim(), CatalogDate = r.CatalogDate, SuggestedValue = r.SuggestedValue, KeywordGroup = r.KeywordGroup?.Trim() ?? string.Empty, Applications = r.Applications?.Trim() ?? string.Empty, CreatedAt = now, UpdatedAt = now };
        db.Parts.Add(entity); await db.SaveChangesAsync(ct); return entity;
    }
    public async Task UpdateAsync(int id, PartRequest r, CancellationToken ct)
    {
        var entity = await db.Parts.FindAsync([id], ct) ?? throw NotFound();
        var oem = NormalizeOem(r.OemPartNumber);
        if (await db.Parts.AnyAsync(x => x.OemPartNumber == oem && x.Id != id, ct)) throw new ApiException(409, "OEM part number already exists.");
        var description = r.Description.Trim();
        var category = r.Category.Trim();
        var metadata = metadataService.Extract(description, category);
        entity.OemPartNumber = oem; entity.Description = description; entity.Category = category; entity.Side = NormalizeText(r.Side) ?? metadata.Side; entity.Position = NormalizeText(r.Position) ?? metadata.Position; entity.SupersededByPartNumber = NormalizeOptional(r.SupersededByPartNumber); entity.Source = r.Source.Trim(); entity.CatalogDate = r.CatalogDate; entity.SuggestedValue = r.SuggestedValue; entity.KeywordGroup = r.KeywordGroup?.Trim() ?? string.Empty; entity.Applications = r.Applications?.Trim() ?? string.Empty; entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await db.Parts.FindAsync([id], ct) ?? throw NotFound(); db.Parts.Remove(entity); await db.SaveChangesAsync(ct);
    }
    public async Task<IReadOnlyList<PartCompatibility>> GetCompatibilitiesAsync(int partId, CancellationToken ct)
    {
        if (!await db.Parts.AnyAsync(x => x.Id == partId, ct)) throw NotFound();
        return await db.PartCompatibilities.AsNoTracking().Include(x => x.Vehicle).Where(x => x.PartId == partId).OrderBy(x => x.Id).ToListAsync(ct);
    }
    public async Task<PartCompatibility> AddCompatibilityAsync(int partId, CompatibilityRequest r, CancellationToken ct)
    {
        if (!await db.Parts.AnyAsync(x => x.Id == partId, ct)) throw NotFound();
        if (!await db.Vehicles.AnyAsync(x => x.Id == r.VehicleId, ct)) throw new ApiException(404, "Vehicle not found.");
        var entity = new PartCompatibility { PartId = partId, VehicleId = r.VehicleId, ProductionStart = r.ProductionStart, ProductionEnd = r.ProductionEnd, Notes = r.Notes?.Trim() };
        db.PartCompatibilities.Add(entity); await db.SaveChangesAsync(ct); return entity;
    }
    private static ApiException NotFound() => new(404, "Part not found.");
    private static string NormalizeOem(string value) => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : NormalizeOem(value);
    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
