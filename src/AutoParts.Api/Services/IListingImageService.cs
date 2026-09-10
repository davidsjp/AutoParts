namespace AutoParts.Api.Services;

public interface IListingImageService
{
    Task<ListingImagesResponse> GenerateAsync(ListingImageRequest request, CancellationToken ct);
    Task<MarketplaceImageResponse> GenerateForMarketplaceAsync(ListingImageRequest request, MarketplaceImageChannel channel, CancellationToken ct);
    Task<IReadOnlyList<string>> GetSavedAsync(string oemPartNumber, CancellationToken ct);
}

public sealed record ListingImageRequest(string OemPartNumber, string Title, string Applications, IReadOnlyList<string> Models, bool ForceRegenerate = false);
public sealed record ListingImagesResponse(string PrimaryImageUrl, string TechnicalImageUrl, string? MarketingImageUrl = null);
public enum MarketplaceImageChannel { MercadoLivre, Shopee }
public sealed record MarketplaceImageResponse(MarketplaceImageChannel Channel, string ImageUrl);
