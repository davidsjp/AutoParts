using AutoParts.Api.Clients;
using AutoParts.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutoParts.Api.Controllers;

/// <summary>
/// Facade for external catalog operations. Import work is delegated to services
/// so controller actions stay limited to validation and HTTP responses.
/// </summary>
[ApiController, Route("api/external-catalog")]
public sealed class ExternalCatalogController(IExternalCatalogService service) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<CatalogSearchResult>>> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(q)] = ["Enter at least 2 characters."] }));
        return Ok(await service.SearchAsync(q, ct));
    }

    [HttpGet("preview/{oemPartNumber}")]
    public async Task<ActionResult<PartImportPreview>> Preview(string oemPartNumber, CancellationToken ct) =>
        Ok(await service.PreviewAsync(oemPartNumber, ct));

    [HttpPost("import/{oemPartNumber}")]
    public async Task<ActionResult<ImportPartResult>> Import(string oemPartNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(oemPartNumber))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(oemPartNumber)] = ["OEM part number is required."] }));
        var result = await service.ImportByOemAsync(oemPartNumber, ct);
        return result.Created
            ? CreatedAtAction(nameof(PartsController.Get), "Parts", new { id = result.PartId }, result)
            : Ok(result);
    }

    [HttpPost("import-car/{externalCarId:int}")]
    public async Task<ActionResult<BulkVehicleImportResult>> ImportCar(
        int externalCarId,
        BulkVehicleImportRequest request,
        CancellationToken ct) => Ok(await service.ImportVehicleCatalogAsync(externalCarId, request, ct));
}
