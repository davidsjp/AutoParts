using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoParts.Api.Infrastructure;

namespace AutoParts.Api.Services;

public sealed class OpenAiListingImageService(HttpClient httpClient, IConfiguration configuration, IWebHostEnvironment environment) : IListingImageService
{
    public async Task<ListingImagesResponse> GenerateAsync(ListingImageRequest input, CancellationToken ct)
    {
        // Amostras aprovadas manualmente: servidas localmente sem uma nova cobrança de geração.
        if (!input.ForceRegenerate && input.OemPartNumber.Equals("51337294828", StringComparison.OrdinalIgnoreCase))
            return new ListingImagesResponse(
                "/images/principal-maquina-vidro-bmw-f20-f21-sem-texto.png",
                "/images/amostra-maquina-vidro-bmw-f20-f21.png");
        if (!input.ForceRegenerate && input.OemPartNumber.Equals("51778051948", StringComparison.OrdinalIgnoreCase))
            return new ListingImagesResponse(
                "/images/principal-spoiler-lateral-bmw-f22-f23.png",
                "/images/tecnica-spoiler-lateral-bmw-f22-f23.png");
        if (!input.ForceRegenerate && input.OemPartNumber.Equals("51217202146", StringComparison.OrdinalIgnoreCase))
            return new ListingImagesResponse(
                "/images/principal-fechadura-porta-bmw-f20-f21.png",
                "/images/tecnica-fechadura-porta-bmw-f20-f21.png");
        if (!input.ForceRegenerate && input.OemPartNumber.Equals("51167366308", StringComparison.OrdinalIgnoreCase))
            return new ListingImagesResponse(
                "/images/principal-retrovisor-bmw-f20-f21.png",
                "/images/tecnica-retrovisor-bmw-f20-f21.png");
        if (!input.ForceRegenerate && input.OemPartNumber.Equals("51327294830", StringComparison.OrdinalIgnoreCase))
            return new ListingImagesResponse(
                "/images/principal-vidro-porta-bmw-f20-f21.png",
                "/images/tecnica-vidro-porta-bmw-f20-f21.png");

        var apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ApiException(503, "OpenAI API key is not configured.");
        var models = string.Join(", ", input.Models.Distinct(StringComparer.OrdinalIgnoreCase));
        var primaryPrompt = $"Create a square 1:1 primary Mercado Livre image for automotive part '{input.Title}', OEM {input.OemPartNumber}. Put the part centered as detailed professional studio product photography on pure white. In the upper-right corner add a compact 2x2 grid of unlabeled vehicle thumbnails showing compatible models only: {models}. Keep the thumbnails secondary and never cover the part. Absolutely no writing, letters, numbers, labels, OEM code, price, watermark, branding, logo, badge, emblem, license plate, packaging or people anywhere in the image, including the vehicle thumbnails. Use plain unbranded plates or hide the plate area. Illustrative commercial render, not manufacturer photography.";
        var technicalPrompt = $"Create a square 1:1 secondary Mercado Livre technical compatibility image for automotive part '{input.Title}', OEM {input.OemPartNumber}. Show the part prominently in foreground and compatible hatchbacks softly defocused in background. Add one clean, legible panel with exactly this text: '{input.Applications}'. No logos, price, watermark or other text. Illustrative commercial render, not manufacturer photography.";
        var folderName = new string(input.OemPartNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var directory = Path.Combine(environment.WebRootPath, "generated", folderName);
        Directory.CreateDirectory(directory);
        var cachedPrimary = Directory.GetFiles(directory, "principal-*.png").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        var cachedTechnical = Directory.GetFiles(directory, "tecnica-*.png").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (!input.ForceRegenerate && cachedPrimary is not null && cachedTechnical is not null)
            return new ListingImagesResponse($"/generated/{folderName}/{Path.GetFileName(cachedPrimary)}", $"/generated/{folderName}/{Path.GetFileName(cachedTechnical)}");
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var primaryFile = $"principal-{stamp}.png";
        var technicalFile = $"tecnica-{stamp}.png";
        await GenerateFileAsync(primaryPrompt, Path.Combine(directory, primaryFile), apiKey, ct);
        await GenerateFileAsync(technicalPrompt, Path.Combine(directory, technicalFile), apiKey, ct);
        return new ListingImagesResponse($"/generated/{folderName}/{primaryFile}", $"/generated/{folderName}/{technicalFile}");
    }

    private async Task GenerateFileAsync(string prompt, string path, string apiKey, CancellationToken ct)
    {
        var body = new { model = configuration["OpenAI:ImageModel"] ?? "gpt-image-2", prompt, size = "1024x1024", quality = "medium", output_format = "png" };
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/images/generations") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new ApiException(502, $"OpenAI image generation failed with HTTP {(int)response.StatusCode}.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var base64 = json.RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString();
        if (string.IsNullOrWhiteSpace(base64)) throw new ApiException(502, "OpenAI returned no image data.");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(base64), ct);
    }
}
