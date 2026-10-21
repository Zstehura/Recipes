namespace RecipesApp.Shared;

/// <summary>
/// The values in <see cref="RecipeFilterPanel"/>: cooking time and servings ranges, and tags.
/// The page that owns the panel keeps one of these, and reads it when it searches.
/// </summary>
public sealed class RecipeFilterState
{
    public int? MinCookTime { get; set; }
    public int? MaxCookTime { get; set; }
    public int? MinServings { get; set; }
    public int? MaxServings { get; set; }
    public HashSet<string> Tags { get; set; } = new();

    /// <summary>
    /// How many filters are on, for the badge on the Filters button. A range counts once
    /// whether one or both ends are set; each selected tag counts on its own.
    /// </summary>
    public int ActiveCount =>
        (MinCookTime.HasValue || MaxCookTime.HasValue ? 1 : 0) +
        (MinServings.HasValue || MaxServings.HasValue ? 1 : 0) +
        Tags.Count;

    public bool IsEmpty => ActiveCount == 0;

    public void ToggleTag(string tag)
    {
        if (!Tags.Remove(tag))
        {
            Tags.Add(tag);
        }
    }

    public void Clear()
    {
        MinCookTime = null;
        MaxCookTime = null;
        MinServings = null;
        MaxServings = null;
        Tags.Clear();
    }
}
