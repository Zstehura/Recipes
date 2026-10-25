namespace RecipesApp.Services.Drafts;

/// <summary>
/// The grocery list page's state: selected recipes and how many batches of each, the filters,
/// and the generated list with what's been ticked off, which is what you're looking at in the
/// shop when you switch apps. Sorted collections keep the serialized form stable, so unchanged
/// state is never re-saved.
/// </summary>
/// <remarks>
/// Older versions saved <see cref="GeneratedList"/> as text. Such a draft no longer reads as
/// JSON for this record, so DraftService discards it, and the page starts fresh once.
/// </remarks>
public sealed record GroceryListDraft(
    SortedDictionary<int, int> Multipliers,
    SortedDictionary<int, string> RecipeNames,
    CombinedGroceryList? GeneratedList,
    string SearchTerm,
    bool ShowFilters,
    int? MinCookTime,
    int? MaxCookTime,
    int? MinServings,
    int? MaxServings,
    List<string> Tags,
    List<string>? GotItems = null)
{
    public const string Key = "grocery-list";
}
