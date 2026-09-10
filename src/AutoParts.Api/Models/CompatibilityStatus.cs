namespace AutoParts.Api.Models;

/// <summary>Confidence state of a part-to-vehicle compatibility.</summary>
public enum CompatibilityStatus
{
    Confirmed,
    Suggested,
    NeedsReview,
    Rejected
}
