using AutoParts.Api.Data;
using AutoParts.Api.Infrastructure;
using AutoParts.Api.Models;
using AutoParts.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Controllers;

/// <summary>
/// Listing preview and generation endpoints used to turn catalog data into
/// marketplace assets. AI output is treated as a suggestion, not as proof of
/// compatibility.
/// </summary>
[ApiController, Route("api/listings")]
public sealed class ListingsController(AutoPartsDbContext db, IListingGenerationService listingGeneration, IListingImageService listingImages, IListingDescriptionService descriptions) : ControllerBase
{
    [HttpGet("{oemPartNumber}")]
    public async Task<ActionResult<ListingSampleResponse>> Get(string oemPartNumber, CancellationToken ct)
    {
        var normalized = new string(oemPartNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var part = await db.Parts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OemPartNumber == normalized, ct);

        if (part is null)
            return NotFound(new ProblemDetails { Title = "Peça não encontrada.", Detail = "Cadastre ou importe o Part Number antes de gerar o anúncio." });

        var vehicles = await db.PartCompatibilities.AsNoTracking()
            .Where(x => x.PartId == part.Id)
            .Where(x => x.Status == CompatibilityStatus.Confirmed)
            .OrderByDescending(x => x.Relevance)
            .Include(x => x.Vehicle)
            .Select(x => new ListingVehicleResponse(
                x.Vehicle.Manufacturer, x.Vehicle.Model, x.Vehicle.Chassis,
                x.Vehicle.Engine, x.ProductionStart, x.ProductionEnd))
            .ToListAsync(ct);

        var applications = vehicles.Count > 0 ? BuildApplications(vehicles) : part.Applications;
        var title = Truncate(part.Description, 60);
        var priceObservations = await db.PartPriceObservations.AsNoTracking()
            .Where(x => x.PartId == part.Id)
            .Select(x => new { x.Amount, x.IsSample })
            .ToListAsync(ct);
        var marketPrices = priceObservations.Any(x => !x.IsSample)
            ? priceObservations.Where(x => !x.IsSample).ToList()
            : priceObservations;
        var prices = marketPrices.Count == 0 ? null : new ListingPriceRangeResponse(
            marketPrices.Min(x => x.Amount), marketPrices.Max(x => x.Amount), marketPrices.Count, marketPrices.All(x => x.IsSample));

        return Ok(new ListingSampleResponse(
            part.OemPartNumber,
            title,
            part.SuggestedValue,
            part.KeywordGroup,
            applications,
            part.Source,
            "https://upload.wikimedia.org/wikipedia/commons/f/f1/2012_BMW_125i_%28F20%29_5-door_hatchback_%282015-07-03%29_01.jpg",
            "https://www.realoem.com/bmw/enUS/select",
            vehicles,
            prices));
    }

    [HttpPost("{oemPartNumber}/generate")]
    public async Task<ActionResult<AiListingSuggestion>> Generate(string oemPartNumber, CancellationToken ct)
    {
        var normalized = new string(oemPartNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var part = await db.Parts.AsNoTracking().FirstOrDefaultAsync(x => x.OemPartNumber == normalized, ct);
        if (part is null) return NotFound(new ProblemDetails { Title = "Peça não encontrada." });

        var compatibilities = await db.PartCompatibilities.AsNoTracking().Where(x => x.PartId == part.Id)
            .Where(x => x.Status == CompatibilityStatus.Confirmed)
            .OrderByDescending(x => x.Relevance)
            .Include(x => x.Vehicle)
            .Select(x => new ListingCompatibilityInput(x.Vehicle.Manufacturer, x.Vehicle.Model, x.Vehicle.Chassis, x.Vehicle.Engine, x.ProductionStart, x.ProductionEnd))
            .ToListAsync(ct);

        var applications = string.Join("; ", compatibilities
            .Select(x => string.Join(" ", new[] { x.Manufacturer, x.Chassis, x.Model, x.Engine }
                .Where(value => !string.IsNullOrWhiteSpace(value))))
            .Distinct());
        if (string.IsNullOrWhiteSpace(applications)) applications = part.Applications;

        try
        {
            return Ok(await listingGeneration.GenerateAsync(new ListingGenerationInput(
                part.OemPartNumber, part.Description, part.Category, part.Source, part.KeywordGroup, applications, compatibilities), ct));
        }
        catch (ApiException exception) when (exception.StatusCode is 429 or 502 or 503)
        {
            return Ok(new AiListingSuggestion(
                Truncate(part.Description, 60),
                part.SuggestedValue,
                part.KeywordGroup,
                applications,
                "requer_conferencia",
                "Sugestão local aplicada: a revisão por IA está indisponível. Confirme o OEM e o VIN no RealOEM antes de publicar."));
        }
    }

    [HttpPost("{oemPartNumber}/images")]
    public async Task<ActionResult<ListingImagesResponse>> GenerateImages(string oemPartNumber, [FromQuery] bool regenerate, CancellationToken ct)
    {
        var normalized = new string(oemPartNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var part = await db.Parts.AsNoTracking().FirstOrDefaultAsync(x => x.OemPartNumber == normalized, ct);
        if (part is null) return NotFound(new ProblemDetails { Title = "Peça não encontrada." });
        var models = await db.PartCompatibilities.AsNoTracking().Where(x => x.PartId == part.Id)
            .Include(x => x.Vehicle).Select(x => x.Vehicle.Model).Distinct().ToListAsync(ct);
        if (models.Count == 0) models = new[] { "116i", "118i", "120i", "125i" }.Where(part.Applications.Contains).ToList();
        return Ok(await listingImages.GenerateAsync(new ListingImageRequest(part.OemPartNumber, part.Description, part.Applications, models, regenerate), ct));
    }

    [HttpPost("{oemPartNumber}/images/{channel}")]
    public async Task<ActionResult<MarketplaceImageResponse>> GenerateMarketplaceImage(string oemPartNumber, string channel, CancellationToken ct)
    {
        if (!Enum.TryParse<MarketplaceImageChannel>(channel, true, out var marketplace))
            return BadRequest(new ProblemDetails { Title = "Canal de imagem invalido.", Detail = "Use MercadoLivre ou Shopee." });

        var normalized = new string(oemPartNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var part = await db.Parts.AsNoTracking().FirstOrDefaultAsync(x => x.OemPartNumber == normalized, ct);
        if (part is null) return NotFound(new ProblemDetails { Title = "Peca nao encontrada." });
        var models = await db.PartCompatibilities.AsNoTracking().Where(x => x.PartId == part.Id)
            .Where(x => x.Status == CompatibilityStatus.Confirmed)
            .Include(x => x.Vehicle).Select(x => x.Vehicle.Model).Distinct().ToListAsync(ct);
        return Ok(await listingImages.GenerateForMarketplaceAsync(
            new ListingImageRequest(part.OemPartNumber, part.Description, part.Applications, models), marketplace, ct));
    }

    [HttpGet("{oemPartNumber}/photos")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetSavedPhotos(string oemPartNumber, CancellationToken ct) =>
        Ok(await listingImages.GetSavedAsync(oemPartNumber, ct));

    [HttpPost("{oemPartNumber}/description")]
    public async Task<ActionResult<ListingDescriptionResponse>> GenerateDescription(string oemPartNumber, CancellationToken ct)
    {
        var normalized = new string(oemPartNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var part = await db.Parts.AsNoTracking().FirstOrDefaultAsync(x => x.OemPartNumber == normalized, ct);
        if (part is null) return NotFound(new ProblemDetails { Title = "Peça não encontrada." });
        var vehicles = await db.PartCompatibilities.AsNoTracking().Where(x => x.PartId == part.Id)
            .Where(x => x.Status == CompatibilityStatus.Confirmed)
            .Include(x => x.Vehicle)
            .Select(x => new ListingVehicleResponse(x.Vehicle.Manufacturer, x.Vehicle.Model, x.Vehicle.Chassis, x.Vehicle.Engine, x.ProductionStart, x.ProductionEnd))
            .ToListAsync(ct);
        var applications = vehicles.Count > 0 ? BuildApplications(vehicles) : part.Applications;
        return Ok(descriptions.Generate(new ListingDescriptionInput(part.OemPartNumber, part.Description, part.Category, part.KeywordGroup, applications, part.Source)));
    }

    private static string BuildApplications(IEnumerable<ListingVehicleResponse> vehicles) => string.Join("; ", vehicles
        .Select(v => string.Join(" ", new[] { v.Manufacturer, v.Chassis, v.Model, v.Engine }.Where(x => !string.IsNullOrWhiteSpace(x)))).Distinct());

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..(length - 1)].TrimEnd() + "…";
}

public sealed record ListingVehicleResponse(string Manufacturer, string Model, string? Chassis, string? Engine, DateOnly? ProductionStart, DateOnly? ProductionEnd);
public sealed record ListingPriceRangeResponse(decimal Minimum, decimal Maximum, int ListingCount, bool IsSample);

public sealed record ListingSampleResponse(
    string OemPartNumber,
    string Title,
    decimal? SuggestedValue,
    string KeywordGroup,
    string Applications,
    string Source,
    string VehicleImageUrl,
    string RealoemUrl,
    IReadOnlyList<ListingVehicleResponse> Compatibilities,
    ListingPriceRangeResponse? PriceRange);
