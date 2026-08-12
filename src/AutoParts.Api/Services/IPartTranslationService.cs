using AutoParts.Api.Clients;

namespace AutoParts.Api.Services;

public interface IPartTranslationService
{
    Task<TranslatedPartData> TranslateAsync(ExternalPart part, CancellationToken ct);
}

public sealed record TranslatedPartData(
    string Title,
    string Category,
    string KeywordGroup,
    string Applications);
