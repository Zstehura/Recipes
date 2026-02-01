using System.ComponentModel.DataAnnotations;

namespace RecipesApp.Models;

public class Ingredient
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ingredient name is required")]
    [StringLength(200, ErrorMessage = "Ingredient name cannot exceed 200 characters")]
    public string Name { get; set; } = string.Empty;

    // Default unit type for this ingredient
    public MeasurementUnit DefaultUnit { get; set; } = MeasurementUnit.Pieces;

    // Navigation property for many-to-many relationship
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}

