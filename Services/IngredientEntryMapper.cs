using RecipesApp.Models;

namespace RecipesApp.Services;

/// <summary>
/// Converts stored recipe lines (quantities in base units: g, ml, pieces) into the
/// <see cref="IngredientEntry"/> form model, using a readable display unit.
/// </summary>
public static class IngredientEntryMapper
{
    public static IngredientEntry FromRecipeIngredient(RecipeIngredient recipeIngredient)
    {
        var (displayQuantity, displayUnit) = UnitConversionService.ConvertFromBaseUnit(
            recipeIngredient.Quantity ?? 0,
            recipeIngredient.Unit ?? UnitConversionService.BASE_COUNT_UNIT);

        return new IngredientEntry
        {
            Name = recipeIngredient.Ingredient.Name,
            // Keep "no quantity" (e.g. salt to taste) as null rather than turning it into 0
            Quantity = recipeIngredient.Quantity.HasValue ? displayQuantity : null,
            Unit = displayUnit,
            Modifier = recipeIngredient.Modifier ?? string.Empty
        };
    }

    public static List<IngredientEntry> FromRecipeIngredients(IEnumerable<RecipeIngredient> recipeIngredients) =>
        recipeIngredients.Select(FromRecipeIngredient).ToList();
}
