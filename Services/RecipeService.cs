using Microsoft.EntityFrameworkCore;
using RecipesApp.Data;
using RecipesApp.Models;

namespace RecipesApp.Services;

public class RecipeService
{
    private readonly RecipeDbContext _context;

    public RecipeService(RecipeDbContext context)
    {
        _context = context;
    }

    public async Task<List<Recipe>> GetAllRecipesAsync()
    {
        return await _context.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .OrderByDescending(r => r.ModifiedDate)
            .ToListAsync();
    }

    public async Task<Recipe?> GetRecipeByIdAsync(int id)
    {
        return await _context.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Recipe> AddRecipeAsync(Recipe recipe, List<IngredientEntry> ingredientEntries)
    {
        recipe.CreatedDate = DateTime.Now;
        recipe.ModifiedDate = DateTime.Now;
        
        // Clear any existing recipe ingredients
        recipe.RecipeIngredients.Clear();
        
        // Process ingredients
        await ProcessIngredientsAsync(recipe, ingredientEntries);
        
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();
        return recipe;
    }

    public async Task<Recipe?> UpdateRecipeAsync(Recipe recipe, List<IngredientEntry> ingredientEntries)
    {
        var existingRecipe = await _context.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == recipe.Id);
            
        if (existingRecipe == null)
            return null;

        existingRecipe.Name = recipe.Name;
        existingRecipe.Instructions = recipe.Instructions;
        existingRecipe.CookingTime = recipe.CookingTime;
        existingRecipe.Servings = recipe.Servings;
        existingRecipe.Tags = recipe.Tags;
        existingRecipe.Notes = recipe.Notes;
        existingRecipe.ModifiedDate = DateTime.Now;

        // Remove existing recipe ingredients
        _context.RecipeIngredients.RemoveRange(existingRecipe.RecipeIngredients);
        existingRecipe.RecipeIngredients.Clear();

        // Process new ingredients
        await ProcessIngredientsAsync(existingRecipe, ingredientEntries);

        await _context.SaveChangesAsync();
        return existingRecipe;
    }

    public async Task<bool> DeleteRecipeAsync(int id)
    {
        var recipe = await _context.Recipes.FindAsync(id);
        if (recipe == null)
            return false;

        _context.Recipes.Remove(recipe);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Recipe>> SearchRecipesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllRecipesAsync();

        var lowerSearchTerm = searchTerm.ToLower();
        return await _context.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .Where(r => r.Name.ToLower().Contains(lowerSearchTerm) ||
                       r.RecipeIngredients.Any(ri => ri.Ingredient.Name.ToLower().Contains(lowerSearchTerm)) ||
                       r.Tags.ToLower().Contains(lowerSearchTerm))
            .OrderByDescending(r => r.ModifiedDate)
            .ToListAsync();
    }

    private async Task ProcessIngredientsAsync(Recipe recipe, List<IngredientEntry> ingredientEntries)
    {
        foreach (var entry in ingredientEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            // Find or create ingredient
            var ingredient = await _context.Ingredients
                .FirstOrDefaultAsync(i => i.Name.ToLower() == entry.Name.ToLower());

            if (ingredient == null)
            {
                ingredient = new Ingredient { Name = entry.Name };
                _context.Ingredients.Add(ingredient);
            }

            // Convert quantity and unit to base unit
            decimal? baseQuantity = null;
            string? baseUnit = null;

            if (entry.Quantity.HasValue)
            {
                var (convertedQuantity, convertedUnit) = UnitConversionService.ConvertToBaseUnit(
                    entry.Quantity.Value, 
                    entry.Unit);
                baseQuantity = convertedQuantity;
                baseUnit = convertedUnit;
            }

            // Add recipe ingredient relationship
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                Recipe = recipe,
                Ingredient = ingredient,
                Quantity = baseQuantity,
                Unit = baseUnit
            });
        }
    }
}

