using AutoParts.Api.Contracts;
using AutoParts.Api.Models;
using AutoParts.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;

namespace AutoParts.Api.Controllers;

/// <summary>
/// CRUD and export endpoints for parts. The catalog endpoints expose a
/// marketplace-oriented projection instead of raw persistence entities.
/// </summary>
[ApiController, Route("api/parts")]
public sealed class PartsController(IPartService service) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<IReadOnlyList<Part>>> GetAll(CancellationToken ct) => Ok(await service.GetAllAsync(ct));
    [HttpGet("catalog")] public async Task<ActionResult<IReadOnlyList<CatalogItemResponse>>> GetCatalog(CancellationToken ct) => Ok(await service.GetCatalogAsync(ct));
    [HttpGet("catalog.tsv")]
    public async Task<IActionResult> DownloadCatalog(CancellationToken ct)
    {
        var items = await service.GetCatalogAsync(ct);
        var text = new StringBuilder("DATA\tCOD. OEM\tTITULO\tVALOR SUGERIDO\tGRUPO DE PALAVRAS CHAVES\tAPLICAÇÕES\r\n");
        foreach (var item in items)
        {
            text.Append(Escape(item.Data?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))).Append('\t')
                .Append(Escape(item.CodOem)).Append('\t').Append(Escape(item.Titulo)).Append('\t')
                .Append(Escape(item.ValorSugerido?.ToString("0.00", CultureInfo.InvariantCulture))).Append('\t')
                .Append(Escape(item.GrupoDePalavrasChaves)).Append('\t').Append(Escape(item.Aplicacoes)).Append("\r\n");
        }
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray(), "text/tab-separated-values; charset=utf-8", "autoparts-catalogo.tsv");
    }
    [HttpGet("{id:int}")] public async Task<ActionResult<Part>> Get(int id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));
    [HttpGet("oem/{oemPartNumber}")] public async Task<ActionResult<Part>> GetByOem(string oemPartNumber, CancellationToken ct) => Ok(await service.GetByOemAsync(oemPartNumber, ct));
    [HttpGet("oem/{oemPartNumber}/compatibility")] public async Task<ActionResult<PartCompatibilityLookupResponse>> GetCompatibilityByOem(string oemPartNumber, CancellationToken ct) => Ok(await service.GetCompatibilityLookupByOemAsync(oemPartNumber, ct));
    [HttpPost] public async Task<ActionResult<Part>> Create(PartRequest request, CancellationToken ct) { var entity = await service.CreateAsync(request, ct); return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity); }
    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, PartRequest request, CancellationToken ct) { await service.UpdateAsync(id, request, ct); return NoContent(); }
    [HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
    [HttpGet("{id:int}/compatibilities")] public async Task<ActionResult<IReadOnlyList<PartCompatibility>>> GetCompatibilities(int id, CancellationToken ct) => Ok(await service.GetCompatibilitiesAsync(id, ct));
    [HttpPost("{id:int}/compatibilities")] public async Task<ActionResult<PartCompatibility>> AddCompatibility(int id, CompatibilityRequest request, CancellationToken ct) { var entity = await service.AddCompatibilityAsync(id, request, ct); return CreatedAtAction(nameof(GetCompatibilities), new { id }, entity); }

    private static string Escape(string? value) => (value ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
