namespace AutoParts.Api.Services;

public interface IListingImageService
{
    Task<ListingImagesResponse> GenerateAsync(ListingImageRequest request, CancellationToken ct);
}

public sealed record ListingImageRequest(string OemPartNumber, string Title, string Applications, IReadOnlyList<string> Models);
public sealed record ListingImagesResponse(string PrimaryImageUrl, string TechnicalImageUrl);
