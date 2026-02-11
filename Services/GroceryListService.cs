using RecipesApp.Models;

namespace RecipesApp.Services;

public class GroceryListService
{
    private readonly RecipeService _recipeService;

    public GroceryListService(RecipeService recipeService)
    {
        _recipeService = recipeService;
    }

    public async Task<string> GenerateGroceryListAsync(Dictionary<int, int> recipeMultipliers)
    {
        if (recipeMultipliers == null || !recipeMultipliers.Any())
        {
            return "No recipes selected.";
        }

        // Fetch all selected recipes
        var recipes = new List<Recipe>();
        foreach (var recipeId in recipeMultipliers.Keys)
        {
            var recipe = await _recipeService.GetRecipeByIdAsync(recipeId);
            if (recipe != null)
            {
                recipes.Add(recipe);
            }
        }

        if (!recipes.Any())
        {
            return "No valid recipes found.";
        }

        // Aggregate ingredients
        var aggregatedIngredients = AggregateIngredients(recipes, recipeMultipliers);

        // Format the grocery list
        return FormatGroceryList(aggregatedIngredients, recipeMultipliers.Count);
    }

    private Dictionary<string, AggregatedIngredient> AggregateIngredients(
        List<Recipe> recipes,
        Dictionary<int, int> recipeMultipliers)
    {
        var ingredientDict = new Dictionary<string, AggregatedIngredient>(StringComparer.OrdinalIgnoreCase);

        foreach (var recipe in recipes)
        {
            var multiplier = recipeMultipliers[recipe.Id];

            foreach (var recipeIngredient in recipe.RecipeIngredients)
            {
                var ingredientName = recipeIngredient.Ingredient.Name;
                var baseUnit = recipeIngredient.Unit ?? UnitConversionService.BASE_COUNT_UNIT;
                var quantity = recipeIngredient.Quantity ?? 0m;
                var adjustedQuantity = quantity * multiplier;

                if (ingredientDict.TryGetValue(ingredientName, out var existing))
                {
                    // Ingredient already exists - add to quantity
                    // Only add if same base unit, otherwise keep separate
                    if (existing.BaseUnit == baseUnit)
                    {
                        existing.TotalQuantity += adjustedQuantity;
                    }
                    else
                    {
                        // Different base units - create a separate entry with unit suffix
                        var uniqueKey = $"{ingredientName}_{baseUnit}";
                        if (!ingredientDict.ContainsKey(uniqueKey))
                        {
                            ingredientDict[uniqueKey] = new AggregatedIngredient
                            {
                                IngredientName = ingredientName,
                                TotalQuantity = adjustedQuantity,
                                BaseUnit = baseUnit
                            };
                        }
                        else
                        {
                            ingredientDict[uniqueKey].TotalQuantity += adjustedQuantity;
                        }
                    }
                }
                else
                {
                    // New ingredient
                    ingredientDict[ingredientName] = new AggregatedIngredient
                    {
                        IngredientName = ingredientName,
                        TotalQuantity = adjustedQuantity,
                        BaseUnit = baseUnit
                    };
                }
            }
        }

        return ingredientDict;
    }

    private string FormatGroceryList(Dictionary<string, AggregatedIngredient> aggregated, int recipeCount)
    {
        var lines = new List<string>();

        lines.Add($"Grocery List for {recipeCount} Recipe{(recipeCount > 1 ? "s" : "")}");
        lines.Add("----------------------------");
        lines.Add("");

        // Sort ingredients alphabetically
        var sortedIngredients = aggregated.Values
            .OrderBy(i => i.IngredientName)
            .ToList();

        foreach (var ingredient in sortedIngredients)
        {
            // Convert from base unit to display-friendly unit
            var (displayQuantity, displayUnit) = UnitConversionService.ConvertFromBaseUnit(
                ingredient.TotalQuantity,
                ingredient.BaseUnit);

            // Get the display name for the unit
            var unitName = UnitConversionService.GetUnitDisplayName(displayUnit);

            // Format quantity with up to 2 decimal places, removing trailing zeros
            var quantityStr = displayQuantity.ToString("0.##");

            // Handle zero or very small quantities
            if (displayQuantity == 0 || displayQuantity < 0.01m)
            {
                lines.Add($"- {ingredient.IngredientName} (trace amount)");
            }
            else
            {
                lines.Add($"- {quantityStr} {unitName} {ingredient.IngredientName}");
            }
        }

        lines.Add("");
        lines.Add("----------------------------");
        lines.Add($"Total: {sortedIngredients.Count} ingredient{(sortedIngredients.Count > 1 ? "s" : "")} from {recipeCount} recipe{(recipeCount > 1 ? "s" : "")}");

        return string.Join(Environment.NewLine, lines);
    }

    private class AggregatedIngredient
    {
        public string IngredientName { get; set; } = string.Empty;
        public decimal TotalQuantity { get; set; }
        public string BaseUnit { get; set; } = string.Empty;
    }
}
