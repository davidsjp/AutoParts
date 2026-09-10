using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoParts.Api.Infrastructure;

namespace AutoParts.Api.Services;

/// <summary>
/// Generates or reuses square listing images. Approved local images and cached
/// generated files are returned before calling the image API to avoid needless
/// cost and drift in established listings.
/// </summary>
public sealed class OpenAiListingImageService(HttpClient httpClient, IConfiguration configuration, IWebHostEnvironment environment) : IListingImageService
{
    public Task<IReadOnlyList<string>> GetSavedAsync(string oemPartNumber, CancellationToken ct)
    {
        var oem = new string(oemPartNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var approved = oem switch
        {
            "51337294828" => new[] { "/images/principal-maquina-vidro-bmw-f20-f21-sem-texto.png", "/images/amostra-maquina-vidro-bmw-f20-f21.png" },
            "51778051948" => new[] { "/images/principal-spoiler-lateral-bmw-f22-f23.png", "/images/tecnica-spoiler-lateral-bmw-f22-f23.png" },
            "51217202146" => new[] { "/images/principal-fechadura-porta-bmw-f20-f21.png", "/images/tecnica-fechadura-porta-bmw-f20-f21.png" },
            "12138616153" => new[] { "/images/principal-bobina-ignicao-bmw-n20-n26.png", "/images/tecnica-bobina-ignicao-bmw-n20-n26.png", "/images/marketing-bobina-ignicao-bmw-n20-n26.png", "/images/mercadolivre-bobina-ignicao-bmw-n20-n26.png", "/images/shopee-bobina-ignicao-bmw-n20-n26.png" },
            "51167366308" => new[] { "/images/principal-retrovisor-bmw-f20-f21.png", "/images/tecnica-retrovisor-bmw-f20-f21.png" },
            "51327294830" => new[] { "/images/principal-vidro-porta-bmw-f20-f21.png", "/images/tecnica-vidro-porta-bmw-f20-f21.png" },
            _ => Array.Empty<string>()
        };
        var directory = Path.Combine(environment.WebRootPath, "generated", oem);
        var generated = Directory.Exists(directory)
            ? new[] { "principal-*.png", "tecnica-*.png", "marketing-*.png" }
                .Select(pattern => Directory.GetFiles(directory, pattern).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault())
                .Where(path => path is not null)
                .Select(path => $"/generated/{oem}/{Path.GetFileName(path!)}").ToList()
            : [];
        var marketplaceImages = Directory.Exists(directory)
            ? new[] { "mercadolivre-*.png", "shopee-*.png" }
                .Select(pattern => Directory.GetFiles(directory, pattern).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault())
                .Where(path => path is not null)
                .Select(path => $"/generated/{oem}/{Path.GetFileName(path!)}")
            : [];
        return Task.FromResult<IReadOnlyList<string>>(approved.Concat(generated).Concat(marketplaceImages).ToList());
    }

    public async Task<MarketplaceImageResponse> GenerateForMarketplaceAsync(ListingImageRequest input, MarketplaceImageChannel channel, CancellationToken ct)
    {
        var apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ApiException(503, "OpenAI API key is not configured.");

        var oem = new string(input.OemPartNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var sourceUrl = (await GetSavedAsync(oem, ct)).FirstOrDefault();
        if (sourceUrl is null) throw new ApiException(404, "Nenhuma imagem salva foi encontrada para este OEM.");
        var sourcePath = Path.Combine(environment.WebRootPath, sourceUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(sourcePath)) throw new ApiException(404, "A imagem base nao esta disponivel no disco.");

        var models = string.Join(", ", input.Models.Distinct(StringComparer.OrdinalIgnoreCase));
        var prompt = channel switch
        {
            MarketplaceImageChannel.MercadoLivre => $"Use the supplied product image as the exact visual reference. Create a square Mercado Livre compliant main product image for '{input.Title}'. Preserve the product accurately, show only one product centered on a pure white background with a subtle natural shadow. Absolutely no text, logo, emblem, badge, watermark, price, vehicle, license plate, packaging, or people. Clean professional marketplace catalog photography.",
            MarketplaceImageChannel.Shopee => $"Use the supplied product image as the exact visual reference. Create a square Shopee product listing image for '{input.Title}'. Preserve the product accurately, use a premium light gray studio background with restrained red accents, and add a clean legible information panel with exactly this text: '{models}'. No price, watermark, ecommerce logo, people, packaging, or additional text. Commercial automotive catalog style.",
            _ => throw new ArgumentOutOfRangeException(nameof(channel))
        };
        var directory = Path.Combine(environment.WebRootPath, "generated", oem);
        Directory.CreateDirectory(directory);
        var fileName = $"{channel.ToString().ToLowerInvariant()}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.png";
        await GenerateEditFileAsync(prompt, sourcePath, Path.Combine(directory, fileName), apiKey, ct);
        return new MarketplaceImageResponse(channel, $"/generated/{oem}/{fileName}");
    }
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
        if (!input.ForceRegenerate && input.OemPartNumber.Equals("12138616153", StringComparison.OrdinalIgnoreCase))
            return new ListingImagesResponse(
                "/images/principal-bobina-ignicao-bmw-n20-n26.png",
                "/images/tecnica-bobina-ignicao-bmw-n20-n26.png",
                "/images/marketing-bobina-ignicao-bmw-n20-n26.png");
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
        // Three-image listing strategy: policy-safe primary, technical fitment, and a trust-focused campaign asset.
        var primaryPrompt = $"Create image 1 of 3: a square 1:1 primary Mercado Livre image for automotive part '{input.Title}', OEM {input.OemPartNumber}. Show only the single part centered as detailed professional studio product photography on pure white with a subtle natural shadow. Absolutely no writing, letters, numbers, labels, OEM code, price, watermark, branding, logo, badge, emblem, vehicle, license plate, packaging or people. Illustrative commercial render, not manufacturer photography.";
        var technicalPrompt = $"Create image 2 of 3: a square 1:1 detailed technical compatibility image for automotive part '{input.Title}', OEM {input.OemPartNumber}. Show the part prominently in foreground and compatible vehicle examples softly defocused in background. Add one clean, legible panel with exactly this text: '{input.Applications}'. No logo, price, watermark or other text. Premium technical automotive catalog design.";
        var marketingPrompt = $"Create image 3 of 3: a square 1:1 trust-focused marketing image for automotive part '{input.Title}', OEM {input.OemPartNumber}. Show the part large in a polished dark automotive studio with restrained red accents. Add a small BMW roundel logo in the upper-right. Add a clean, bold Portuguese text panel with exactly these lines: 'PECA 100% ORIGINAL', 'TESTADA', 'COM GARANTIA'. Make all text fully legible. No price, watermark, people, packaging, or additional text.";
        var folderName = new string(input.OemPartNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var directory = Path.Combine(environment.WebRootPath, "generated", folderName);
        Directory.CreateDirectory(directory);
        var cachedPrimary = Directory.GetFiles(directory, "principal-*.png").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        var cachedTechnical = Directory.GetFiles(directory, "tecnica-*.png").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        var cachedMarketing = Directory.GetFiles(directory, "marketing-*.png").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (!input.ForceRegenerate && cachedPrimary is not null && cachedTechnical is not null && cachedMarketing is not null)
            return new ListingImagesResponse($"/generated/{folderName}/{Path.GetFileName(cachedPrimary)}", $"/generated/{folderName}/{Path.GetFileName(cachedTechnical)}", $"/generated/{folderName}/{Path.GetFileName(cachedMarketing)}");
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var primaryFile = $"principal-{stamp}.png";
        var technicalFile = $"tecnica-{stamp}.png";
        var marketingFile = $"marketing-{stamp}.png";
        await GenerateFileAsync(primaryPrompt, Path.Combine(directory, primaryFile), apiKey, ct);
        await GenerateFileAsync(technicalPrompt, Path.Combine(directory, technicalFile), apiKey, ct);
        await GenerateFileAsync(marketingPrompt, Path.Combine(directory, marketingFile), apiKey, ct);
        return new ListingImagesResponse($"/generated/{folderName}/{primaryFile}", $"/generated/{folderName}/{technicalFile}", $"/generated/{folderName}/{marketingFile}");
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

    private async Task GenerateEditFileAsync(string prompt, string sourcePath, string outputPath, string apiKey, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(configuration["OpenAI:ImageModel"] ?? "gpt-image-2"), "model");
        form.Add(new StringContent(prompt), "prompt");
        form.Add(new StringContent("1024x1024"), "size");
        form.Add(new StringContent("medium"), "quality");
        form.Add(new StringContent("png"), "output_format");
        await using var image = File.OpenRead(sourcePath);
        using var imageContent = new StreamContent(image);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(imageContent, "image", Path.GetFileName(sourcePath));
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/images/edits") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new ApiException(502, $"OpenAI image edit failed with HTTP {(int)response.StatusCode}.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var base64 = json.RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString();
        if (string.IsNullOrWhiteSpace(base64)) throw new ApiException(502, "OpenAI returned no image data.");
        await File.WriteAllBytesAsync(outputPath, Convert.FromBase64String(base64), ct);
    }
}
