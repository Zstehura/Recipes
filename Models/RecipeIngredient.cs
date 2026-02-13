namespace RecipesApp.Models;

public class RecipeIngredient
{
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

