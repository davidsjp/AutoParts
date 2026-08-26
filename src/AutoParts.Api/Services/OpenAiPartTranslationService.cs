using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoParts.Api.Clients;
using AutoParts.Api.Infrastructure;

namespace AutoParts.Api.Services;

/// <summary>
/// Uses OpenAI Responses API to translate raw external part data into a compact
/// Brazilian Portuguese catalog record. The prompt forbids invented
/// compatibility details; callers should still verify important listings.
/// </summary>
public sealed class OpenAiPartTranslationService(HttpClient httpClient, IConfiguration configuration) : IPartTranslationService
{
    public async Task<TranslatedPartData> TranslateAsync(ExternalPart part, CancellationToken ct)
    {
        var apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ApiException(503, "OpenAI API key is not configured.");

        var source = new
        {
            oem = part.PartNumberClean,
            description = part.Description,
            additionalInfo = part.AdditionalInfo,
            vehicles = part.Vehicles.DistinctBy(x => x.CarId).Select(x => new
            {
                name = x.CarName,
                x.Chassis,
                x.Engine,
                x.BodyType,
                x.YearStart,
                x.YearEnd,
                x.CategoryName,
                x.SubcategoryName
            })
        };

        var stringSchema = new Dictionary<string, object> { ["type"] = "string" };
        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object>
            {
                ["title"] = stringSchema,
                ["category"] = stringSchema,
                ["keywordGroup"] = stringSchema,
                ["applications"] = stringSchema
            },
            ["required"] = new[] { "title", "category", "keywordGroup", "applications" }
        };
        var requestBody = new
        {
            model = configuration["OpenAI:Model"] ?? "gpt-5-mini",
            instructions = "Translate and normalize BMW parts catalog data to Brazilian Portuguese. Preserve OEM numbers, chassis codes, engine codes, model names and years exactly. Create a concise marketplace title, a short category, lowercase search keywords without invented compatibility, and a semicolon-separated applications string. Never invent facts absent from the source.",
            input = JsonSerializer.Serialize(source),
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "translated_part",
                    strict = true,
                    schema
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/responses")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(502, $"OpenAI translation failed with HTTP {(int)response.StatusCode}.");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var outputText = document.RootElement.GetProperty("output").EnumerateArray()
            .Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "message")
            .SelectMany(x => x.GetProperty("content").EnumerateArray())
            .Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "output_text")
            .Select(x => x.GetProperty("text").GetString())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? throw new ApiException(502, "OpenAI returned no translated content.");

        var translated = JsonSerializer.Deserialize<TranslationPayload>(outputText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new ApiException(502, "OpenAI returned an invalid translation.");
        return new TranslatedPartData(translated.Title.Trim(), translated.Category.Trim(),
            translated.KeywordGroup.Trim(), translated.Applications.Trim());
    }

    private sealed record TranslationPayload(string Title, string Category, string KeywordGroup, string Applications);
}
