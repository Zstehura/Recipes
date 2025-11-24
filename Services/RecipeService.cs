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
            .OrderByDescending(r => r.ModifiedDate)
            .ToListAsync();
    }

    public async Task<Recipe?> GetRecipeByIdAsync(int id)
    {
        return await _context.Recipes.FindAsync(id);
    }

    public async Task<Recipe> AddRecipeAsync(Recipe recipe)
    {
        recipe.CreatedDate = DateTime.Now;
        recipe.ModifiedDate = DateTime.Now;
        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();
        return recipe;
    }

    public async Task<Recipe?> UpdateRecipeAsync(Recipe recipe)
    {
        var existingRecipe = await _context.Recipes.FindAsync(recipe.Id);
        if (existingRecipe == null)
            return null;

        existingRecipe.Name = recipe.Name;
        existingRecipe.Ingredients = recipe.Ingredients;
        existingRecipe.Instructions = recipe.Instructions;
        existingRecipe.CookingTime = recipe.CookingTime;
        existingRecipe.Servings = recipe.Servings;
        existingRecipe.Tags = recipe.Tags;
        existingRecipe.Notes = recipe.Notes;
        existingRecipe.ModifiedDate = DateTime.Now;

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
            .Where(r => r.Name.ToLower().Contains(lowerSearchTerm) ||
                       r.Ingredients.ToLower().Contains(lowerSearchTerm) ||
                       r.Tags.ToLower().Contains(lowerSearchTerm))
            .OrderByDescending(r => r.ModifiedDate)
            .ToListAsync();
    }
}

