using RecipesApp.Models;

namespace RecipesApp.Services;

/// <summary>
/// Converts stored recipe lines into the <see cref="IngredientEntry"/> form model, in the unit
/// they were entered in.
/// </summary>
public static class IngredientEntryMapper
{
    public static IngredientEntry FromRecipeIngredient(RecipeIngredient recipeIngredient)
    {
        var unit = recipeIngredient.Unit ?? MeasurementUnit.Pieces;
        return new IngredientEntry
        {
            Name = recipeIngredient.Ingredient.Name,
            // Keep "no quantity" (e.g. salt to taste) as null rather than turning it into 0
            Quantity = recipeIngredient.Quantity is decimal quantity ? ForEditing(quantity, unit) : null,
            Unit = unit,
            Modifier = recipeIngredient.Modifier ?? string.Empty
        };
    }

    public static List<IngredientEntry> FromRecipeIngredients(IEnumerable<RecipeIngredient> recipeIngredients) =>
        recipeIngredients.Select(FromRecipeIngredient).ToList();

    /// <summary>
    /// The number as the edit form should show it: "2", not "2.0" (SQLite hands decimals back
    /// with trailing zeros). Grams and milliliters are rounded to two decimal places, so lines
    /// saved when everything was converted to base units show "14.79" ml rather than "14.7868";
    /// a hundredth of a gram is finer than any kitchen measures. Other units keep their
    /// precision, so "0.125 cup" stays as entered.
    /// </summary>
    public static decimal ForEditing(decimal quantity, MeasurementUnit unit)
    {
        var shown = unit is MeasurementUnit.Grams or MeasurementUnit.Milliliters
            ? Math.Round(quantity, 2, MidpointRounding.AwayFromZero)
            : quantity;
        // Dividing by 1 with many zeros drops trailing zeros from a decimal's scale (2.000m -> 2m)
        return shown / 1.000000000000000000000000000000000m;
    }
}
