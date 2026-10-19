namespace RecipesApp.Services.Drafts;

/// <summary>
/// The grocery list page's state: selected recipes and how many batches of each, the filters,
/// and the generated list, which is what you're looking at in the shop when you switch apps.
/// Sorted collections keep the serialized form stable, so unchanged state is never re-saved.
/// </summary>
public sealed record GroceryListDraft(
    SortedDictionary<int, int> Multipliers,
    SortedDictionary<int, string> RecipeNames,
    string? GeneratedList,
    string SearchTerm,
    bool ShowFilters,
    int? MinCookTime,
    int? MaxCookTime,
    int? MinServings,
    int? MaxServings,
    List<string> Tags)
{
    public const string Key = "grocery-list";
}
