using RecipesApp.Models;

namespace RecipesApp.Services;

/// <summary>
/// Read-only access to a single recipe. Consumers that only need to look up
/// recipes (such as <see cref="GroceryListService"/>) depend on this narrow
/// interface instead of the full <see cref="RecipeService"/>, which also lets
/// tests substitute a fake.
/// </summary>
public interface IRecipeReader
{
    Task<Recipe?> GetRecipeByIdAsync(int id);
}
