using AutoParts.Api.Clients;
using AutoParts.Api.Data;
using AutoParts.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Controllers;

/// <summary>Read-only catalogue used by the vehicle selection screen.</summary>
[ApiController]
[Route("api/vehicle-catalog")]
public sealed class VehicleCatalogController(
    AutoPartsDbContext db,
    IPartsCatalogClient catalog,
    IVehicleImageService vehicleImages) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, string> ReferenceImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["E53"] = "https://commons.wikimedia.org/wiki/Special:FilePath/BMW_E53_front_20090104.jpg?width=960",
        ["E87"] = "https://upload.wikimedia.org/wikipedia/commons/3/32/BMW_E87_front_20080719.jpg",
        ["F20"] = "https://commons.wikimedia.org/wiki/Special:FilePath/00_BMW_F20_1.jpg?width=960",
        ["F30"] = "https://commons.wikimedia.org/wiki/Special:FilePath/BMW_320d_(F30%2C_2017)_(52712386032).jpg?width=960",
        ["F10"] = "https://upload.wikimedia.org/wikipedia/commons/a/a7/BMW_535i_%28F10%29_front_20100425.jpg",
        ["G30"] = "https://upload.wikimedia.org/wikipedia/commons/thumb/d/db/BMW_G30_530e_IMG_3862.jpg/1280px-BMW_G30_530e_IMG_3862.jpg"
    };
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleCardResponse>>> GetVehicles(
        [FromQuery] string? search,
        [FromQuery] int take = 24,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 60);
        var normalized = search?.Trim();
        var query = db.Vehicles.AsNoTracking().Where(vehicle => vehicle.Compatibilities.Count >= 10);

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            var term = $"%{normalized}%";
            query = query.Where(vehicle =>
                EF.Functions.Like(vehicle.Manufacturer, term) ||
                EF.Functions.Like(vehicle.Model, term) ||
                (vehicle.Chassis != null && EF.Functions.Like(vehicle.Chassis, term)) ||
                (vehicle.Engine != null && EF.Functions.Like(vehicle.Engine, term)) ||
                (vehicle.TypeCode != null && EF.Functions.Like(vehicle.TypeCode, term)));
        }

        var vehicles = await query
            .OrderBy(vehicle => vehicle.Manufacturer).ThenBy(vehicle => vehicle.Chassis).ThenBy(vehicle => vehicle.Model)
            .Take(take)
            .Select(vehicle => new VehicleRow(
                vehicle.Id, vehicle.Manufacturer, vehicle.Model, vehicle.Chassis, vehicle.Engine,
                vehicle.ModelYear, vehicle.Market, vehicle.TypeCode, vehicle.SerialNumber,
                vehicle.Compatibilities.Count))
            .ToListAsync(ct);

        var cards = await Task.WhenAll(vehicles.Select(async vehicle => new VehicleCardResponse(
            vehicle.Id, vehicle.Manufacturer, vehicle.Model, vehicle.Chassis, vehicle.Engine,
            vehicle.ModelYear, vehicle.Market, vehicle.TypeCode, vehicle.PartCount,
            await GetCatalogImageAsync(vehicle.SerialNumber, vehicle.Chassis, ct))));

        return Ok(cards);
    }

    [HttpGet("{vehicleId:int}/parts")]
    public async Task<ActionResult<VehicleCatalogPartsPageResponse>> GetParts(
        int vehicleId,
        [FromQuery] string? search,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 100);
        var vehicleExists = await db.Vehicles.AsNoTracking().AnyAsync(vehicle => vehicle.Id == vehicleId, ct);
        if (!vehicleExists) return NotFound();

        var query = db.PartCompatibilities.AsNoTracking()
            .Where(compatibility => compatibility.VehicleId == vehicleId)
            .Where(compatibility => !EF.Functions.Like(compatibility.Part.Category, "Ignorar%"))
            .Where(compatibility => compatibility.Part.Category != "Sem categoria (revisar)")
            .Where(compatibility => compatibility.Part.Category != "Manuais e documentos")
            // Catalogue instructions and internal notes are not commercial auto parts.
            .Where(compatibility =>
                !EF.Functions.Like(compatibility.Part.Description, "Template") &&
                !EF.Functions.Like(compatibility.Part.Description, "Read comments%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Consulte as observações%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Operating instructions%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Manual de instruções%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Installation instructions%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Mounting information%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Installation information%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Form, airbag%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Etiqueta%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Labels%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Adhesive Etiqueta%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Tag") &&
                !EF.Functions.Like(compatibility.Part.Description, "%instructions%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%handbook%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%literature%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%Access request%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%Claim Form%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%radio pass%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%booklet%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%Care tips%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%Logbook%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%owner% manual%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%owner% handbook%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%Supplem.% manual%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%tag%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%labels%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%Insertion sheet%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%Documentation%") &&
                !EF.Functions.Like(compatibility.Part.Description, "%certificate%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Parafuso%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Porca%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Arruela%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Screw%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Nut%") &&
                !EF.Functions.Like(compatibility.Part.Description, "Washer%"));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(compatibility =>
                EF.Functions.Like(compatibility.Part.OemPartNumber, term) ||
                EF.Functions.Like(compatibility.Part.Description, term) ||
                EF.Functions.Like(compatibility.Part.Category, term));
        }

        var total = await query.CountAsync(ct);
        var parts = await query
            .OrderBy(compatibility => compatibility.Part.OemPartNumber)
            .Skip(skip).Take(take)
            .Select(compatibility => new VehicleCatalogPartResponse(
                compatibility.Part.Id,
                compatibility.Part.OemPartNumber,
                compatibility.Part.Description,
                compatibility.Part.Category,
                compatibility.Part.Source,
                compatibility.Part.Applications,
                compatibility.Part.Side,
                compatibility.Part.Position,
                compatibility.Vehicle.Manufacturer,
                compatibility.Vehicle.Model,
                compatibility.Vehicle.Chassis,
                compatibility.Vehicle.Engine,
                compatibility.Vehicle.TypeCode,
                compatibility.ProductionStart,
                compatibility.ProductionEnd))
            .ToListAsync(ct);

        return Ok(new VehicleCatalogPartsPageResponse(total, skip, take, parts));
    }

    private async Task<string?> GetCatalogImageAsync(string? serialNumber, string? chassis, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(serialNumber) && serialNumber.StartsWith("BMV", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(serialNumber[3..], out var externalCarId))
        {
            try
            {
                var catalogImage = (await catalog.GetCarAsync(externalCarId, ct))?.ImageUrl;
                if (!string.IsNullOrWhiteSpace(catalogImage)) return catalogImage;
            }
            catch
            {
                // A catalogue image is decorative; an external timeout must not hide local parts.
            }
        }

        var chassisCode = chassis?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(chassisCode) && chassisCode.Length >= 3
            && ReferenceImages.TryGetValue(chassisCode[..3], out var fallbackReferenceImage))
            return fallbackReferenceImage;

        try
        {
            var referenceImage = await vehicleImages.FindAsync(chassis, null, ct);
            if (referenceImage is not null) return referenceImage.ImageUrl;
        }
        catch
        {
            // A reference image is decorative; the vehicle catalog must stay usable offline.
        }

        return null;
    }

    private sealed record VehicleRow(
        int Id, string Manufacturer, string Model, string? Chassis, string? Engine,
        int ModelYear, string? Market, string? TypeCode, string? SerialNumber, int PartCount);
}

public sealed record VehicleCardResponse(
    int Id, string Manufacturer, string Model, string? Chassis, string? Engine,
    int ModelYear, string? Market, string? TypeCode, int PartCount, string? ImageUrl);

public sealed record VehicleCatalogPartResponse(
    int Id, string OemPartNumber, string Description, string Category, string Source,
    string Applications, string? Side, string? Position, string Manufacturer, string Model,
    string? Chassis, string? Engine, string? TypeCode, DateOnly? ProductionStart, DateOnly? ProductionEnd);

public sealed record VehicleCatalogPartsPageResponse(
    int Total, int Skip, int Take, IReadOnlyList<VehicleCatalogPartResponse> Items);
