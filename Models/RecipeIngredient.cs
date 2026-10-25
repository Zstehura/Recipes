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

    // Quantity and unit as entered ("2 tbsp"), so the recipe shows and edits the way it was
    // written. They're converted to base units only where amounts are added up (the grocery
    // list). Lines saved before this was the case are in grams, milliliters, or pieces.
    // No quantity (e.g. "salt to taste") means no unit either.
    public decimal? Quantity { get; set; }

    // Stored by name ("Tablespoons") in the existing text column
    public MeasurementUnit? Unit { get; set; }

    // Preparation modifier (e.g., "sliced", "diced", "chopped")
    public string? Modifier { get; set; }
}

