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
    public async Task<IReadOnlyList<PartCategoryResponse>> GetCategoriesAsync(CancellationToken ct) =>
        await db.Parts.AsNoTracking()
            .Where(x => !EF.Functions.Like(x.Category, "Ignorar%"))
            .Where(x => x.Category != "Sem categoria (revisar)")
            .GroupBy(x => x.Category)
            .Select(group => new PartCategoryResponse(group.Key, group.Count()))
            .OrderByDescending(x => x.PartCount).ThenBy(x => x.Category)
            .ToListAsync(ct);
    public async Task<IReadOnlyList<Part>> GetByCategoryAsync(string category, CancellationToken ct) =>
        await db.Parts.AsNoTracking()
            .Where(x => x.Category == category.Trim())
            .OrderBy(x => x.Description).ThenBy(x => x.OemPartNumber)
            .Take(100)
            .ToListAsync(ct);
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
                    compatibility.Notes,
                    compatibility.Relevance,
                    compatibility.Status,
                    compatibility.Confidence,
                    compatibility.Source,
                    compatibility.EvidenceText);
            })
            .OrderByDescending(x => StatusRank(x.Status))
            .ThenByDescending(x => x.Relevance)
            .ThenByDescending(x => x.Confidence)
            .ThenBy(x => x.Manufacturer)
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
                        model.Max(x => x.Relevance),
                        model.OrderByDescending(x => StatusRank(x.Status)).ThenByDescending(x => x.Relevance).First().Status,
                        model.Select(x => x.VehicleId).Distinct().Count(),
                        model.GroupBy(x => new { x.Chassis, x.Engine, x.TypeCode, x.Market })
                            .Select(version => new CompatibleVersionResponse(
                                version.Key.Chassis,
                                version.Key.Engine,
                                version.Key.TypeCode,
                                version.Key.Market,
                                version.Min(x => x.YearStart),
                                version.Max(x => x.YearEnd ?? x.YearStart),
                                version.Select(x => x.VehicleId).Distinct().Count(),
                                version.Max(x => x.Relevance),
                                version.OrderByDescending(x => StatusRank(x.Status)).ThenByDescending(x => x.Relevance).First().Status,
                                version.Max(x => x.Confidence)))
                            .OrderByDescending(x => StatusRank(x.Status))
                            .ThenByDescending(x => x.Relevance)
                            .ThenBy(x => x.Chassis)
                            .ThenBy(x => x.Engine)
                            .ThenBy(x => x.YearStart)
                            .ToList()))
                    .OrderByDescending(x => StatusRank(x.Status))
                    .ThenByDescending(x => x.Relevance)
                    .ThenBy(x => x.Model)
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
        return await db.PartCompatibilities.AsNoTracking().Include(x => x.Vehicle).Where(x => x.PartId == partId)
            .OrderBy(x => x.Status == CompatibilityStatus.Confirmed ? 0 : x.Status == CompatibilityStatus.Suggested ? 1 : x.Status == CompatibilityStatus.NeedsReview ? 2 : 3)
            .ThenByDescending(x => x.Relevance).ThenByDescending(x => x.Confidence).ThenBy(x => x.Id).ToListAsync(ct);
    }
    public async Task<PartCompatibility> AddCompatibilityAsync(int partId, CompatibilityRequest r, CancellationToken ct)
    {
        if (!await db.Parts.AnyAsync(x => x.Id == partId, ct)) throw NotFound();
        if (!await db.Vehicles.AnyAsync(x => x.Id == r.VehicleId, ct)) throw new ApiException(404, "Vehicle not found.");
        if (await db.PartCompatibilities.AnyAsync(x => x.PartId == partId && x.VehicleId == r.VehicleId && x.ProductionStart == r.ProductionStart, ct))
            throw new ApiException(409, "Compatibility already exists for this part, vehicle and production start.");

        var now = DateTimeOffset.UtcNow;
        var entity = new PartCompatibility
        {
            PartId = partId,
            VehicleId = r.VehicleId,
            ProductionStart = r.ProductionStart,
            ProductionEnd = r.ProductionEnd,
            Notes = NormalizeText(r.Notes),
            Relevance = r.Relevance,
            Status = r.Status,
            Confidence = r.Confidence,
            Source = NormalizeText(r.Source) ?? "Manual",
            EvidenceText = NormalizeText(r.EvidenceText),
            ConfirmedByUserId = r.Status == CompatibilityStatus.Confirmed ? NormalizeText(r.ConfirmedByUserId) : null,
            ConfirmedAt = r.Status == CompatibilityStatus.Confirmed ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.PartCompatibilities.Add(entity);
        AddReviewLog(entity, "Created", null, null, r.Status, r.Relevance, NormalizeText(r.ConfirmedByUserId), NormalizeText(r.Notes), now);
        await db.SaveChangesAsync(ct);
        return entity;
    }
    public async Task<PartCompatibility> UpdateCompatibilityAsync(int partId, int compatibilityId, CompatibilityUpdateRequest r, CancellationToken ct)
    {
        var entity = await db.PartCompatibilities.Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == compatibilityId && x.PartId == partId, ct)
            ?? throw new ApiException(404, "Compatibility not found.");

        var now = DateTimeOffset.UtcNow;
        var previousStatus = entity.Status;
        var previousRelevance = entity.Relevance;
        entity.ProductionStart = r.ProductionStart;
        entity.ProductionEnd = r.ProductionEnd;
        entity.Notes = NormalizeText(r.Notes);
        entity.Relevance = r.Relevance;
        entity.Status = r.Status;
        entity.Confidence = r.Confidence;
        entity.Source = NormalizeText(r.Source) ?? entity.Source;
        entity.EvidenceText = NormalizeText(r.EvidenceText);
        entity.ConfirmedByUserId = r.Status == CompatibilityStatus.Confirmed ? NormalizeText(r.ConfirmedByUserId) : null;
        entity.ConfirmedAt = r.Status == CompatibilityStatus.Confirmed ? now : null;
        entity.UpdatedAt = now;
        AddReviewLog(entity, GetAction(previousStatus, r.Status), previousStatus, previousRelevance, r.Status, r.Relevance,
            NormalizeText(r.ConfirmedByUserId), NormalizeText(r.Notes), now);
        await db.SaveChangesAsync(ct);
        return entity;
    }
    public async Task<IReadOnlyList<CompatibilityReviewLog>> GetCompatibilityReviewLogsAsync(int partId, int compatibilityId, CancellationToken ct)
    {
        if (!await db.PartCompatibilities.AnyAsync(x => x.Id == compatibilityId && x.PartId == partId, ct))
            throw new ApiException(404, "Compatibility not found.");
        var logs = await db.CompatibilityReviewLogs.AsNoTracking()
            .Where(x => x.PartCompatibilityId == compatibilityId)
            .ToListAsync(ct);
        return logs.OrderByDescending(x => x.PerformedAt).ToList();
    }
    private void AddReviewLog(PartCompatibility compatibility, string action, CompatibilityStatus? previousStatus,
        int? previousRelevance, CompatibilityStatus newStatus, int newRelevance, string? performedByUserId,
        string? notes, DateTimeOffset performedAt) => db.CompatibilityReviewLogs.Add(new CompatibilityReviewLog
        {
            PartCompatibility = compatibility,
            Action = action,
            PreviousStatus = previousStatus,
            PreviousRelevance = previousRelevance,
            NewStatus = newStatus,
            NewRelevance = newRelevance,
            PerformedByUserId = performedByUserId,
            Notes = notes,
            PerformedAt = performedAt
        });
    private static int StatusRank(CompatibilityStatus status) => status switch
    {
        CompatibilityStatus.Confirmed => 4,
        CompatibilityStatus.Suggested => 3,
        CompatibilityStatus.NeedsReview => 2,
        CompatibilityStatus.Rejected => 1,
        _ => 0
    };
    private static string GetAction(CompatibilityStatus previous, CompatibilityStatus current) =>
        current == CompatibilityStatus.Confirmed && previous != CompatibilityStatus.Confirmed ? "Confirmed" :
        current == CompatibilityStatus.Rejected && previous != CompatibilityStatus.Rejected ? "Rejected" :
        current == CompatibilityStatus.NeedsReview && previous != CompatibilityStatus.NeedsReview ? "MarkedForReview" :
        "Updated";
    private static ApiException NotFound() => new(404, "Part not found.");
    private static string NormalizeOem(string value) => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : NormalizeOem(value);
    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
