namespace RecipesApp.Services;

/// <summary>
/// Tags are stored on a recipe as one comma-separated string (e.g. "Italian, Dinner").
/// This is the single place that turns that string into individual tags, so the tag list
/// shown in the UI and the tag filter always agree.
/// </summary>
public static class RecipeTags
{
    public static IReadOnlyList<string> Parse(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return Array.Empty<string>();

        return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>True if the recipe's tags contain any of the wanted tags (case-insensitive, whole tags only).</summary>
    public static bool ContainsAny(string? tags, IEnumerable<string> wanted)
    {
        var recipeTags = Parse(tags);
        return wanted.Any(w => recipeTags.Contains(w.Trim(), StringComparer.OrdinalIgnoreCase));
    }
}
