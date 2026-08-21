namespace AutoParts.Api.Services;

public interface IVehicleImageService
{
    Task<VehicleImageReference?> FindAsync(string? chassis, string? model, CancellationToken ct);
}

public sealed record VehicleImageReference(string ImageUrl, string SourceUrl);
