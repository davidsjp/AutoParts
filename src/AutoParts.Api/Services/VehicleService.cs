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

    public async Task<Vehicle> CreateAsync(VehicleRequest r, CancellationToken ct)
    {
        var entity = new Vehicle { Manufacturer = r.Manufacturer.Trim(), Model = r.Model.Trim(), Chassis = r.Chassis?.Trim(), Engine = r.Engine?.Trim(), ModelYear = r.ModelYear, ProductionDate = r.ProductionDate, Vin = Normalize(r.Vin) };
        db.Vehicles.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(int id, VehicleRequest r, CancellationToken ct)
    {
        var entity = await db.Vehicles.FindAsync([id], ct) ?? throw new ApiException(404, "Vehicle not found.");
        entity.Manufacturer = r.Manufacturer.Trim(); entity.Model = r.Model.Trim(); entity.Chassis = r.Chassis?.Trim();
        entity.Engine = r.Engine?.Trim(); entity.ModelYear = r.ModelYear; entity.ProductionDate = r.ProductionDate; entity.Vin = Normalize(r.Vin);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await db.Vehicles.FindAsync([id], ct) ?? throw new ApiException(404, "Vehicle not found.");
        db.Vehicles.Remove(entity); await db.SaveChangesAsync(ct);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
