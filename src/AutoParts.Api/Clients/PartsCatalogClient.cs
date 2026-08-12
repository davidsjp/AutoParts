using System.Net;
using System.Net.Http.Json;

namespace AutoParts.Api.Clients;

public sealed class PartsCatalogClient(HttpClient httpClient) : IPartsCatalogClient
{
    public async Task<IReadOnlyList<CatalogSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var path = $"api/search?q={Uri.EscapeDataString(query.Trim())}";
        return await httpClient.GetFromJsonAsync<List<CatalogSearchResult>>(path, cancellationToken) ?? [];
    }

    public async Task<ExternalPart?> FindByOemNumberAsync(string oemPartNumber, CancellationToken cancellationToken = default)
    {
        var clean = new string(oemPartNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        using var response = await httpClient.GetAsync($"api/parts/cross-reference/{Uri.EscapeDataString(clean)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExternalPart>(cancellationToken: cancellationToken);
    }

    public async Task<ExternalCar?> GetCarAsync(int carId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/cars/{carId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExternalCar>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ExternalCarPart>> GetCarPartsAsync(int carId, CancellationToken cancellationToken = default)
    {
        var page = await httpClient.GetFromJsonAsync<ExternalCarPartsPage>(
            $"api/cars/{carId}/parts?limit=10000&offset=0", cancellationToken);
        return page?.Parts ?? [];
    }
}
