namespace AutoParts.Api.Services;

public interface IListingDescriptionService
{
    ListingDescriptionResponse Generate(ListingDescriptionInput input);
}

public sealed record ListingDescriptionInput(string OemPartNumber, string Title, string Category, string KeywordGroup, string Applications, string Source);
public sealed record ListingDescriptionResponse(string Description, string Generator, string VerificationNote);
