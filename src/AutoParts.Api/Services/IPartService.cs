using AutoParts.Api.Contracts;
using AutoParts.Api.Models;

namespace AutoParts.Api.Services;

public interface IPartService
{
    Task<IReadOnlyList<Part>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<CatalogItemResponse>> GetCatalogAsync(CancellationToken ct);
    Task<IReadOnlyList<PartCategoryResponse>> GetCategoriesAsync(CancellationToken ct);
    Task<IReadOnlyList<Part>> GetByCategoryAsync(string category, CancellationToken ct);
    Task<Part> GetAsync(int id, CancellationToken ct);
    Task<Part> GetByOemAsync(string oem, CancellationToken ct);
    Task<PartCompatibilityLookupResponse> GetCompatibilityLookupByOemAsync(string oem, CancellationToken ct);
    Task<Part> CreateAsync(PartRequest request, CancellationToken ct);
    Task UpdateAsync(int id, PartRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<PartCompatibility>> GetCompatibilitiesAsync(int partId, CancellationToken ct);
    Task<PartCompatibility> AddCompatibilityAsync(int partId, CompatibilityRequest request, CancellationToken ct);
    Task<PartCompatibility> UpdateCompatibilityAsync(int partId, int compatibilityId, CompatibilityUpdateRequest request, CancellationToken ct);
    Task<IReadOnlyList<CompatibilityReviewLog>> GetCompatibilityReviewLogsAsync(int partId, int compatibilityId, CancellationToken ct);
}
