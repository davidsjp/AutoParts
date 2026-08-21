using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace AutoParts.Api.Services;

/// <summary>Finds a real, chassis-specific reference photo from Wikimedia Commons.</summary>
public sealed class WikimediaVehicleImageService(HttpClient client, IMemoryCache cache) : IVehicleImageService
{
    private static readonly Regex ChassisCode = new(@"\b([EFG]\d{2})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly SemaphoreSlim RequestGate = new(2, 2);

    public async Task<VehicleImageReference?> FindAsync(string? chassis, string? model, CancellationToken ct)
    {
        var code = ChassisCode.Match(chassis ?? string.Empty).Groups[1].Value.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return null;

        return await cache.GetOrCreateAsync($"wikimedia-bmw-{code}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14);
            var query = Uri.EscapeDataString($"BMW {code}");
            var path = $"w/api.php?action=query&format=json&generator=search&gsrnamespace=6&gsrsearch={query}&gsrlimit=10&prop=imageinfo&iiprop=url&iiurlwidth=960";
            await RequestGate.WaitAsync(ct);
            HttpResponseMessage? response = null;
            try
            {
                response = await client.GetAsync(path, ct);
                if (!response.IsSuccessStatusCode) return null;

                using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
                if (!document.RootElement.TryGetProperty("query", out var queryResult)
                    || !queryResult.TryGetProperty("pages", out var pages)) return null;

                var candidate = pages.EnumerateObject()
                    .Select(page => page.Value)
                    .Select(page => new
                    {
                        Title = page.TryGetProperty("title", out var title) ? title.GetString() : null,
                        Image = page.TryGetProperty("imageinfo", out var imageInfo) && imageInfo.GetArrayLength() > 0
                            && imageInfo[0].TryGetProperty("thumburl", out var thumbnail) ? thumbnail.GetString() : null
                    })
                    .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Image)
                        && item.Title?.Contains(code, StringComparison.OrdinalIgnoreCase) == true);

                if (candidate?.Image is null || candidate.Title is null) return null;
                return new VehicleImageReference(candidate.Image,
                    $"https://commons.wikimedia.org/wiki/{Uri.EscapeDataString(candidate.Title)}");
            }
            finally
            {
                response?.Dispose();
                RequestGate.Release();
            }
        });
    }
}
