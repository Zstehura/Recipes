using RecipesApp.Services;

namespace RecipesApp.Services.Drafts;

/// <summary>Recipe search filters, shared by the recipe list and the search-by-ingredients page.</summary>
public sealed record RecipeFilters(
    string SearchTerm,
    bool ShowFilters,
    int? MinCookTime,
    int? MaxCookTime,
    int? MinServings,
    int? MaxServings,
    List<string> Tags)
{
    /// <summary>Key for the recipe list page's filters.</summary>
    public const string RecipeListKey = "recipe-list-filters";
}

/// <summary>
/// The search-by-ingredients page: which ingredients are selected, the filters, and how the
/// ingredients match (drafts saved before there was a choice read as "uses all").
/// </summary>
public sealed record IngredientSearchDraft(
    List<int> SelectedIngredientIds,
    RecipeFilters Filters,
    IngredientMatch Match = IngredientMatch.UsesAll)
{
    public const string Key = "ingredient-search";
}
