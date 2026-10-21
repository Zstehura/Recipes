using RecipesApp.Models;

namespace RecipesApp.Services.Drafts;

/// <summary>Which ingredients are crossed off in a recipe's cooking view.</summary>
public sealed record CookingProgress(DateTime RecipeModifiedDate, List<int> DoneIngredientIds)
{
    public static string KeyFor(int recipeId) => $"cooking:{recipeId}";

    public static CookingProgress Capture(Recipe recipe, IEnumerable<int> doneIngredientIds) =>
        new(recipe.ModifiedDate, doneIngredientIds.Order().ToList());

    /// <summary>Progress saved before the recipe was edited may point at lines that changed.</summary>
    public bool IsStaleFor(Recipe recipe) => RecipeModifiedDate != recipe.ModifiedDate;
}
