using System.ComponentModel.DataAnnotations;

namespace RecipesApp.Models;

public class Recipe
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Recipe name is required")]
    [StringLength(200, ErrorMessage = "Recipe name cannot exceed 200 characters")]
    public string Name { get; set; } = string.Empty;

    // Navigation property for many-to-many relationship with ingredients
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();

    [Required(ErrorMessage = "Instructions are required")]
    [StringLength(20000, ErrorMessage = "Instructions cannot exceed 20,000 characters")]
    public string Instructions { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Cooking time must be at least 1 minute")]
    public int CookingTime { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Servings must be at least 1")]
    public int Servings { get; set; }

    [StringLength(500, ErrorMessage = "Tags cannot exceed 500 characters")]
    public string Tags { get; set; } = string.Empty;

    [StringLength(10000, ErrorMessage = "Notes cannot exceed 10,000 characters")]
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime ModifiedDate { get; set; } = DateTime.Now;

    // Optional background image stored as BLOB
    public byte[]? ImageData { get; set; }

    // Image content type (e.g., "image/jpeg", "image/png")
    public string? ImageContentType { get; set; }
}

