using RecipesApp.Models;

namespace RecipesApp.Services.Drafts;

/// <summary>
/// The recipe form's unsaved state. Photos aren't included: they can be up to 10MB, far more
/// than browser storage should hold, so a photo has to be chosen again after a restore.
/// </summary>
/// <param name="BaseModifiedDate">
/// For an existing recipe, its ModifiedDate when editing started. If the recipe has been saved
/// since (for example from another device), the draft is out of date and is discarded.
/// </param>
public sealed record RecipeDraft(
    string Name,
    string Instructions,
    int CookingTime,
    int Servings,
    string Tags,
    string Notes,
    List<IngredientEntry> Ingredients,
    DateTime? BaseModifiedDate)
{
    public static string KeyFor(int recipeId) => recipeId > 0 ? $"recipe-draft:{recipeId}" : "recipe-draft:new";

    public static RecipeDraft Capture(Recipe recipe, IEnumerable<IngredientEntry> entries, DateTime? baseModifiedDate) => new(
        recipe.Name,
        recipe.Instructions,
        recipe.CookingTime,
        recipe.Servings,
        recipe.Tags,
        recipe.Notes,
        entries.Select(Copy).ToList(),
        baseModifiedDate);

    /// <summary>True if this draft was started from a different version of the recipe than <paramref name="current"/>.</summary>
    public bool IsStaleFor(Recipe current) => current.Id > 0 && BaseModifiedDate != current.ModifiedDate;

    /// <summary>Copies the draft's fields onto the recipe and returns its ingredient entries.</summary>
    public List<IngredientEntry> ApplyTo(Recipe recipe)
    {
        recipe.Name = Name;
        recipe.Instructions = Instructions;
        recipe.CookingTime = CookingTime;
        recipe.Servings = Servings;
        recipe.Tags = Tags;
        recipe.Notes = Notes;
        return Ingredients.Select(Copy).ToList();
    }

    // Copies, so the draft never shares IngredientEntry instances with the form
    private static IngredientEntry Copy(IngredientEntry entry) => new()
    {
        Name = entry.Name,
        Quantity = entry.Quantity,
        Unit = entry.Unit,
        Modifier = entry.Modifier,
    };
}
