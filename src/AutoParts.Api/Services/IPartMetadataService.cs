namespace AutoParts.Api.Services;

public interface IPartMetadataService
{
    PartMetadata Extract(string description, string category);
}

public sealed record PartMetadata(string? Side, string? Position);
