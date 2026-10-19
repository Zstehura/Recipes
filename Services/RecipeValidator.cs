using System.ComponentModel.DataAnnotations;
using RecipesApp.Models;

namespace RecipesApp.Services;

/// <summary>
/// Server-side validation for a recipe and its ingredient entries, using the data annotation
/// attributes on the models. The form shows the same recipe rules as you type, but every
/// save path (form, import, anything added later) goes through RecipeService, so this is
/// where the rules are actually enforced. SQLite doesn't enforce column lengths itself.
/// </summary>
public static class RecipeValidator
{
    /// <exception cref="ValidationException">The first rule that fails, with a user-facing message.</exception>
    public static void Validate(Recipe recipe, IEnumerable<IngredientEntry> ingredientEntries)
    {
        Validator.ValidateObject(recipe, new ValidationContext(recipe), validateAllProperties: true);

        foreach (var entry in ingredientEntries)
        {
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(entry, new ValidationContext(entry), results, validateAllProperties: true))
            {
                throw new ValidationException($"Ingredient \"{Shorten(entry.Name)}\": {results[0].ErrorMessage}");
            }
        }
    }

    // The name itself may be the problem (too long), so keep the message readable
    private static string Shorten(string name) => name.Length <= 40 ? name : name[..40] + "...";
}
