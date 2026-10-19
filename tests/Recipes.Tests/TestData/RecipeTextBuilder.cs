namespace Recipes.Tests.TestData;

/// <summary>
/// Builds recipe text in the import format so tests only spell out the parts they care about.
/// </summary>
public static class RecipeTextBuilder
{
    public static string Build(
        string? name = "Pancakes",
        string? cookingTime = "20 minutes",
        string? servings = "4",
        string? tags = null,
        string? created = null,
        string[]? ingredients = null,
        string? instructions = "Mix and cook.",
        string? notes = null,
        bool includeEndDelimiter = true)
    {
        var lines = new List<string> { "===== RECIPE =====" };
        if (name != null) lines.Add($"Name: {name}");
        if (cookingTime != null) lines.Add($"Cooking Time: {cookingTime}");
        if (servings != null) lines.Add($"Servings: {servings}");
        if (tags != null) lines.Add($"Tags: {tags}");
        if (created != null) lines.Add($"Created: {created}");
        lines.Add("");

        lines.Add("===== INGREDIENTS =====");
        lines.AddRange(ingredients ?? new[] { "250g flour" });
        lines.Add("");

        if (instructions != null)
        {
            lines.Add("===== INSTRUCTIONS =====");
            lines.Add(instructions);
            lines.Add("");
        }

        if (notes != null)
        {
            lines.Add("===== NOTES =====");
            lines.Add(notes);
            lines.Add("");
        }

        if (includeEndDelimiter) lines.Add("===== END RECIPE =====");

        return string.Join("\n", lines);
    }
}
