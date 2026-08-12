namespace AutoParts.Api.Services;

public interface IListingGenerationService
{
    Task<AiListingSuggestion> GenerateAsync(ListingGenerationInput input, CancellationToken ct);
}

public sealed record ListingGenerationInput(string OemPartNumber, string Description, string Category, string Source, string ExistingKeywords, string ExistingApplications, IReadOnlyList<ListingCompatibilityInput> Compatibilities);
public sealed record ListingCompatibilityInput(string Manufacturer, string Model, string? Chassis, string? Engine, DateOnly? ProductionStart, DateOnly? ProductionEnd);
public sealed record AiListingSuggestion(string Title, decimal? SuggestedValue, string KeywordGroup, string Applications, string CompatibilityStatus, string VerificationNote);
