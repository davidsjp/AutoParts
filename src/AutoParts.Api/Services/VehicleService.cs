using AutoParts.Api.Contracts;
using AutoParts.Api.Data;
using AutoParts.Api.Infrastructure;
using AutoParts.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Services;

public sealed class VehicleService(AutoPartsDbContext db) : IVehicleService
{
    public async Task<IReadOnlyList<Vehicle>> GetAllAsync(CancellationToken ct) =>
        await db.Vehicles.AsNoTracking().OrderBy(x => x.Manufacturer).ThenBy(x => x.Model).ToListAsync(ct);

    public async Task<Vehicle> GetAsync(int id, CancellationToken ct) =>
        await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ApiException(404, "Vehicle not found.");

    public async Task<VehicleChassisLookupResponse> ResolveByChassisAsync(string chassisOrSerial, CancellationToken ct)
    {
        var query = chassisOrSerial.Trim().ToUpperInvariant();
        if (query.Length < 2) throw new ApiException(400, "Chassis or serial must contain at least 2 characters.");

        var vehicles = await db.Vehicles.AsNoTracking()
            .Where(x =>
                (x.Chassis != null && x.Chassis.ToUpper() == query) ||
                (x.SerialNumber != null && x.SerialNumber.ToUpper() == query) ||
                (x.Vin != null && x.Vin.EndsWith(query)) ||
                (x.SerialNumber != null && x.SerialNumber.ToUpper().StartsWith(query)))
            .OrderBy(x => x.Manufacturer).ThenBy(x => x.Model).ThenBy(x => x.Chassis)
            .Select(x => new VehicleChassisMatchResponse(
                x.Id,
                x.Manufacturer,
                x.Model,
                x.Chassis,
                x.SerialNumber,
                x.Engine,
                x.ModelYear,
                x.MarketRelevance,
                10,
                x.SerialNumber != null && x.SerialNumber.ToUpper() == query ? "SerialNumberExact" :
                x.Chassis != null && x.Chassis.ToUpper() == query ? "ChassisExact" :
                x.Vin != null && x.Vin.EndsWith(query) ? "VinSuffix" : "SerialNumberPrefix"))
            .ToListAsync(ct);

        return new VehicleChassisLookupResponse(query, vehicles);
    }

    public async Task<Vehicle> CreateAsync(VehicleRequest r, CancellationToken ct)
    {
        var entity = new Vehicle { Manufacturer = r.Manufacturer.Trim(), Model = r.Model.Trim(), Chassis = r.Chassis?.Trim(), Engine = r.Engine?.Trim(), ModelYear = r.ModelYear, ProductionDate = r.ProductionDate, Vin = Normalize(r.Vin), SerialNumber = NormalizeSerial(r.SerialNumber), Market = NormalizeText(r.Market), TypeCode = NormalizeText(r.TypeCode), MarketRelevance = r.MarketRelevance ?? 0, MarketRelevanceSource = NormalizeText(r.MarketRelevanceSource), MarketRelevanceUpdatedAt = r.MarketRelevanceSource is null ? null : DateTimeOffset.UtcNow };
        db.Vehicles.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(int id, VehicleRequest r, CancellationToken ct)
    {
        var entity = await db.Vehicles.FindAsync([id], ct) ?? throw new ApiException(404, "Vehicle not found.");
        entity.Manufacturer = r.Manufacturer.Trim(); entity.Model = r.Model.Trim(); entity.Chassis = r.Chassis?.Trim();
        entity.Engine = r.Engine?.Trim(); entity.ModelYear = r.ModelYear; entity.ProductionDate = r.ProductionDate; entity.Vin = Normalize(r.Vin); entity.SerialNumber = NormalizeSerial(r.SerialNumber); entity.Market = NormalizeText(r.Market); entity.TypeCode = NormalizeText(r.TypeCode);
        if (r.MarketRelevance.HasValue) entity.MarketRelevance = r.MarketRelevance.Value;
        if (r.MarketRelevanceSource is not null)
        {
            entity.MarketRelevanceSource = NormalizeText(r.MarketRelevanceSource);
            entity.MarketRelevanceUpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await db.Vehicles.FindAsync([id], ct) ?? throw new ApiException(404, "Vehicle not found.");
        db.Vehicles.Remove(entity); await db.SaveChangesAsync(ct);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static string? NormalizeSerial(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
