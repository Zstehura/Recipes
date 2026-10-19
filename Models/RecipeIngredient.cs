namespace RecipesApp.Models;

public class RecipeIngredient
{
    // Surrogate key, so the same ingredient can appear more than once in a recipe
    // (e.g. "tomato (sliced)" and "tomato (diced)")
    public int Id { get; set; }

    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    // Quantity stored in base unit (grams for weight, milliliters for volume, pieces for count)
    public decimal? Quantity { get; set; }

    // Unit stored as base unit string ("g", "ml", or "pieces")
    public string? Unit { get; set; }

    // Preparation modifier (e.g., "sliced", "diced", "chopped")
    public string? Modifier { get; set; }
}

