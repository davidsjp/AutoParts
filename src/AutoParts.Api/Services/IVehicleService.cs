using AutoParts.Api.Contracts;
using AutoParts.Api.Models;

namespace AutoParts.Api.Services;

public interface IVehicleService
{
    Task<IReadOnlyList<Vehicle>> GetAllAsync(CancellationToken ct);
    Task<Vehicle> GetAsync(int id, CancellationToken ct);
    Task<VehicleChassisLookupResponse> ResolveByChassisAsync(string chassisOrSerial, CancellationToken ct);
    Task<Vehicle> CreateAsync(VehicleRequest request, CancellationToken ct);
    Task UpdateAsync(int id, VehicleRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}
