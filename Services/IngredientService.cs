using Microsoft.EntityFrameworkCore;
using RecipesApp.Data;
using RecipesApp.Models;

namespace RecipesApp.Services;

public class IngredientService
{
    private readonly RecipeDbContext _context;

    public IngredientService(RecipeDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Normalizes an ingredient name by trimming whitespace and capitalizing the first letter.
    /// </summary>
    public static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        var trimmed = name.Trim();
        return char.ToUpper(trimmed[0]) + trimmed[1..];
    }

    public async Task<List<Ingredient>> GetAllIngredientsAsync()
    {
        return await _context.Ingredients
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<Ingredient?> GetIngredientByIdAsync(int id)
    {
        return await _context.Ingredients
            .Include(i => i.RecipeIngredients)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<Ingredient?> GetIngredientByNameAsync(string name)
    {
        return await _context.Ingredients
            .FirstOrDefaultAsync(i => i.Name.ToLower() == name.ToLower());
    }

    public async Task<bool> RenameIngredientAsync(int id, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return false;

        var ingredient = await _context.Ingredients
            .FirstOrDefaultAsync(i => i.Id == id);

        if (ingredient == null)
            return false;

        // Check if another ingredient with the same name already exists
        var existingIngredient = await GetIngredientByNameAsync(newName);
        if (existingIngredient != null && existingIngredient.Id != id)
        {
            // If an ingredient with this name exists, merge instead
            return false;
        }

        ingredient.Name = NormalizeName(newName);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangeDefaultUnitAsync(int id, MeasurementUnit newUnit)
    {
        var ingredient = await _context.Ingredients
            .FirstOrDefaultAsync(i => i.Id == id);

        if (ingredient == null)
            return false;

        ingredient.DefaultUnit = newUnit;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MergeIngredientsAsync(int targetId, int sourceId)
    {
        if (targetId == sourceId)
            return false;

        var targetIngredient = await _context.Ingredients
            .FirstOrDefaultAsync(i => i.Id == targetId);

        var sourceIngredient = await _context.Ingredients
            .FirstOrDefaultAsync(i => i.Id == sourceId);

        if (targetIngredient == null || sourceIngredient == null)
            return false;

        // Get all recipe ingredients that use the source ingredient
        var sourceRecipeIngredients = await _context.RecipeIngredients
            .Where(ri => ri.IngredientId == sourceId)
            .ToListAsync();

        // Update all recipe ingredients to use the target ingredient
        foreach (var recipeIngredient in sourceRecipeIngredients)
        {
            // Check if the recipe already has the target ingredient
            var existingRecipeIngredient = await _context.RecipeIngredients
                .FirstOrDefaultAsync(ri => ri.RecipeId == recipeIngredient.RecipeId && 
                                          ri.IngredientId == targetId);

            if (existingRecipeIngredient != null)
            {
                // If the recipe already has the target ingredient, remove the source ingredient entry
                _context.RecipeIngredients.Remove(recipeIngredient);
            }
            else
            {
                // Since RecipeIngredient has a composite key, we need to remove the old one
                // and create a new one with the target ingredient
                var newRecipeIngredient = new RecipeIngredient
                {
                    RecipeId = recipeIngredient.RecipeId,
                    IngredientId = targetId,
                    Quantity = recipeIngredient.Quantity,
                    Unit = recipeIngredient.Unit,
                    Modifier = recipeIngredient.Modifier
                };
                
                // Remove the old recipe ingredient
                _context.RecipeIngredients.Remove(recipeIngredient);
                
                // Add the new recipe ingredient
                _context.RecipeIngredients.Add(newRecipeIngredient);
            }
        }

        // Save changes to update all RecipeIngredients first
        await _context.SaveChangesAsync();

        // Verify no RecipeIngredients still reference the source ingredient
        var remainingRecipeIngredients = await _context.RecipeIngredients
            .AnyAsync(ri => ri.IngredientId == sourceId);
        
        if (remainingRecipeIngredients)
        {
            // If there are still references, something went wrong
            return false;
        }

        // Reload the source ingredient without navigation properties to avoid tracking issues
        _context.Entry(sourceIngredient).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        var ingredientToRemove = await _context.Ingredients
            .FirstOrDefaultAsync(i => i.Id == sourceId);
        
        if (ingredientToRemove != null)
        {
            // Remove the source ingredient
            _context.Ingredients.Remove(ingredientToRemove);
            await _context.SaveChangesAsync();
        }
        
        return true;
    }

    public async Task<bool> DeleteIngredientAsync(int id)
    {
        var ingredient = await _context.Ingredients
            .FirstOrDefaultAsync(i => i.Id == id);

        if (ingredient == null)
            return false;

        _context.Ingredients.Remove(ingredient);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetIngredientUsageCountAsync(int id)
    {
        return await _context.RecipeIngredients
            .Where(ri => ri.IngredientId == id)
            .CountAsync();
    }

    public async Task<List<Recipe>> GetRecipesUsingIngredientAsync(int id)
    {
        return await _context.Recipes
            .Where(r => r.RecipeIngredients.Any(ri => ri.IngredientId == id))
            .Select(r => new Recipe
            {
                Id = r.Id,
                Name = r.Name,
                // Exclude ImageData for performance
                ImageData = null
            })
            .ToListAsync();
    }
}
