using System.Text.RegularExpressions;

namespace AutoParts.Api.Services;

/// <summary>
/// Extracts searchable part attributes from marketplace titles. This is
/// intentionally conservative: unknown attributes stay null instead of guessing.
/// </summary>
public sealed class PartMetadataService : IPartMetadataService
{
    public PartMetadata Extract(string description, string category)
    {
        var text = $"{description} {category}".ToLowerInvariant();
        return new PartMetadata(ExtractSide(text), ExtractPosition(text));
    }

    private static string? ExtractSide(string value)
    {
        var hasLeft = HasAny(value, "esquerdo", "esquerda", "left", "lh", "lado esquerdo");
        var hasRight = HasAny(value, "direito", "direita", "right", "rh", "lado direito");
        if (hasLeft && hasRight) return "Par";
        if (hasLeft) return "Esquerda";
        if (hasRight) return "Direita";
        return null;
    }

    private static string? ExtractPosition(string value)
    {
        var hasFront = HasAny(value, "dianteiro", "dianteira", "front");
        var hasRear = HasAny(value, "traseiro", "traseira", "rear");
        var hasUpper = HasAny(value, "superior", "upper");
        var hasLower = HasAny(value, "inferior", "lower");
        if (hasFront && hasRear) return "Dianteira/Traseira";
        if (hasFront) return "Dianteira";
        if (hasRear) return "Traseira";
        if (hasUpper) return "Superior";
        if (hasLower) return "Inferior";
        return null;
    }

    private static bool HasAny(string value, params string[] terms) =>
        terms.Any(term => Regex.IsMatch(value, $@"(?<![a-z0-9]){Regex.Escape(term)}(?![a-z0-9])",
            RegexOptions.CultureInvariant));
}
