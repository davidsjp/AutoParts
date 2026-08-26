using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoParts.Api.Infrastructure;

namespace AutoParts.Api.Services;

/// <summary>
/// Produces structured listing suggestions from persisted evidence. The schema
/// and prompt force a verification note so marketplace publishing remains a
/// human-confirmed step.
/// </summary>
public sealed class OpenAiListingGenerationService(HttpClient httpClient, IConfiguration configuration) : IListingGenerationService
{
    public async Task<AiListingSuggestion> GenerateAsync(ListingGenerationInput input, CancellationToken ct)
    {
        var apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ApiException(503, "OpenAI API key is not configured.");
        var schema = new Dictionary<string, object>
        {
            ["type"] = "object", ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object>
            {
                ["title"] = new { type = "string" }, ["suggestedValue"] = new { type = new[] { "number", "null" } },
                ["keywordGroup"] = new { type = "string" }, ["applications"] = new { type = "string" },
                ["compatibilityStatus"] = new { type = "string", @enum = new[] { "confirmada", "requer_conferencia" } }, ["verificationNote"] = new { type = "string" }
            },
            ["required"] = new[] { "title", "suggestedValue", "keywordGroup", "applications", "compatibilityStatus", "verificationNote" }
        };
        const string instructions = """
Atue como especialista técnico brasileiro em anúncios de autopeças BMW. Você recebe somente um Part Number e evidências cadastradas. Nunca invente OEM, chassis, motor, ano, lado, posição, acabamento ou compatibilidades.
Valide o modelo exclusivamente com compatibilities e source: se source não for exatamente 'RealOEM.com', compatibilityStatus deve ser 'requer_conferencia' e a nota deve pedir conferência no RealOEM por VIN/type code antes de publicar. Quando confirmado, use 'confirmada'.
Gere title em português, máximo 60 caracteres, no padrão '[Peça] BMW [carroceria] [modelos] [anos curtos]'. Preserve códigos e anos existentes. applications deve listar somente compatibilidades fornecidas, com anos por extenso quando houver intervalos. keywordGroup deve conter termos técnicos e populares em minúsculas.
suggestedValue é apenas estimativa em BRL. Sem preços de mercado, devolva null. verificationNote deve ser curta e em português.
""";
        var requestBody = new { model = configuration["OpenAI:Model"] ?? "gpt-5-mini", instructions, input = JsonSerializer.Serialize(input), text = new { format = new { type = "json_schema", name = "listing_suggestion", strict = true, schema } } };
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/responses") { Content = JsonContent.Create(requestBody) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new ApiException(502, $"OpenAI listing generation failed with HTTP {(int)response.StatusCode}.");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var outputText = document.RootElement.GetProperty("output").EnumerateArray().Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "message").SelectMany(x => x.GetProperty("content").EnumerateArray()).Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "output_text").Select(x => x.GetProperty("text").GetString()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? throw new ApiException(502, "OpenAI returned no listing content.");
        var result = JsonSerializer.Deserialize<AiListingSuggestion>(outputText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new ApiException(502, "OpenAI returned an invalid listing.");
        if (result.Title.Trim().Length > 60) throw new ApiException(502, "OpenAI returned a title longer than 60 characters.");
        return result with { Title = result.Title.Trim(), KeywordGroup = result.KeywordGroup.Trim(), Applications = result.Applications.Trim(), VerificationNote = result.VerificationNote.Trim() };
    }
}
