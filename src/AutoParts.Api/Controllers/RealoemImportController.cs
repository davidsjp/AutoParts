using AutoParts.Api.Data;
using AutoParts.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Controllers;

[ApiController, Route("api/realoem")]
public sealed class RealoemImportController(AutoPartsDbContext db) : ControllerBase
{
    [HttpPost("import")]
    public async Task<ActionResult<RealoemImportResponse>> Import(RealoemImportRequest request, CancellationToken ct)
    {
        var oem = new string((request.OemPartNumber ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(oem)) return BadRequest("Informe o Part Number OEM.");
        var source = Clean(request.CopiedData);
        if (string.IsNullOrWhiteSpace(source)) return BadRequest("Cole os dados da peça copiados do RealOEM.");

        var lines = source.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var description = Clean(request.Description);
        if (string.IsNullOrWhiteSpace(description))
            description = lines.FirstOrDefault(x => !x.Contains(oem, StringComparison.OrdinalIgnoreCase) && x.Length is > 3 and < 500) ?? $"Peça BMW OEM {oem}";
        var applications = Clean(request.Applications);
        if (string.IsNullOrWhiteSpace(applications)) applications = source.Length > 1000 ? source[..1000] : source;
        var keywords = Clean(request.KeywordGroup);
        if (string.IsNullOrWhiteSpace(keywords)) keywords = string.Join(' ', description.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct());

        var part = await db.Parts.FirstOrDefaultAsync(x => x.OemPartNumber == oem, ct);
        var now = DateTimeOffset.UtcNow;
        if (part is null)
        {
            part = new Part { OemPartNumber = oem, Description = description, Category = "Peça OEM", Source = "RealOEM.com", KeywordGroup = keywords, Applications = applications, CreatedAt = now, UpdatedAt = now };
            db.Parts.Add(part);
        }
        else
        {
            part.Description = description; part.Category = "Peça OEM"; part.Source = "RealOEM.com"; part.KeywordGroup = keywords; part.Applications = applications; part.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
        return Ok(new RealoemImportResponse(part.OemPartNumber, part.Description, part.Applications, "Dados importados como RealOEM.com. Use “IA revisar e sugerir” para finalizar o título, as palavras-chave e o anúncio."));
    }

    private static string Clean(string? value) => (value ?? string.Empty).Trim().Replace("\r\n", "\n");
}

public sealed record RealoemImportRequest(string OemPartNumber, string CopiedData, string? Description, string? Applications, string? KeywordGroup);
public sealed record RealoemImportResponse(string OemPartNumber, string Description, string Applications, string Message);
