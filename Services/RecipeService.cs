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
            .Select(r => new Recipe
            {
                Id = r.Id,
                Name = r.Name,
                Instructions = r.Instructions,
                CookingTime = r.CookingTime,
                Servings = r.Servings,
                Tags = r.Tags,
                Notes = r.Notes,
                CreatedDate = r.CreatedDate,
                ModifiedDate = r.ModifiedDate,
                ImageContentType = r.ImageContentType,
                RecipeIngredients = r.RecipeIngredients,
                // Exclude ImageData for performance
                ImageData = null
            })
            .OrderByDescending(r => r.ModifiedDate)
            .ToListAsync();
    }

    public async Task<Recipe?> GetRecipeByIdAsync(int id)
    {
        return await _context.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .Select(r => new Recipe
            {
                Id = r.Id,
                Name = r.Name,
                Instructions = r.Instructions,
                CookingTime = r.CookingTime,
                Servings = r.Servings,
                Tags = r.Tags,
                Notes = r.Notes,
                CreatedDate = r.CreatedDate,
                ModifiedDate = r.ModifiedDate,
                ImageContentType = r.ImageContentType,
                RecipeIngredients = r.RecipeIngredients,
                // Exclude ImageData for performance
                ImageData = null
            })
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

    public async Task<Recipe?> UpdateRecipeAsync(Recipe recipe, List<IngredientEntry> ingredientEntries, bool removeImage = false)
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
        
        // Update or remove image
        if (removeImage)
        {
            existingRecipe.ImageData = null;
            existingRecipe.ImageContentType = null;
        }
        else if (recipe.ImageData != null)
        {
            existingRecipe.ImageData = recipe.ImageData;
            existingRecipe.ImageContentType = recipe.ImageContentType;
        }

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
            .Select(r => new Recipe
            {
                Id = r.Id,
                Name = r.Name,
                Instructions = r.Instructions,
                CookingTime = r.CookingTime,
                Servings = r.Servings,
                Tags = r.Tags,
                Notes = r.Notes,
                CreatedDate = r.CreatedDate,
                ModifiedDate = r.ModifiedDate,
                ImageContentType = r.ImageContentType,
                RecipeIngredients = r.RecipeIngredients,
                // Exclude ImageData for performance
                ImageData = null
            })
            .OrderByDescending(r => r.ModifiedDate)
            .ToListAsync();
    }

    public async Task<List<Recipe>> FilterRecipesAsync(
        string? searchTerm = null,
        int? minCookTime = null,
        int? maxCookTime = null,
        List<string>? tags = null,
        int? minServings = null,
        int? maxServings = null)
    {
        var query = _context.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .AsQueryable();

        // Apply search term filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearchTerm = searchTerm.ToLower();
            query = query.Where(r => r.Name.ToLower().Contains(lowerSearchTerm) ||
                                   r.RecipeIngredients.Any(ri => ri.Ingredient.Name.ToLower().Contains(lowerSearchTerm)) ||
                                   r.Tags.ToLower().Contains(lowerSearchTerm));
        }

        // Apply cook time filters
        if (minCookTime.HasValue)
        {
            query = query.Where(r => r.CookingTime >= minCookTime.Value);
        }
        if (maxCookTime.HasValue)
        {
            query = query.Where(r => r.CookingTime <= maxCookTime.Value);
        }

        // Apply serving count filters
        if (minServings.HasValue)
        {
            query = query.Where(r => r.Servings >= minServings.Value);
        }
        if (maxServings.HasValue)
        {
            query = query.Where(r => r.Servings <= maxServings.Value);
        }

        // Apply tag filters (matches if recipe contains any of the selected tags)
        if (tags != null && tags.Any())
        {
            var lowerTags = tags.Select(t => t.ToLower().Trim()).ToList();
            query = query.Where(r => !string.IsNullOrWhiteSpace(r.Tags) && 
                lowerTags.Any(tag => 
                    r.Tags.ToLower().Contains("," + tag + ",") ||
                    r.Tags.ToLower().StartsWith(tag + ",") ||
                    r.Tags.ToLower().EndsWith("," + tag) ||
                    r.Tags.ToLower() == tag));
        }

        return await query
            .Select(r => new Recipe
            {
                Id = r.Id,
                Name = r.Name,
                Instructions = r.Instructions,
                CookingTime = r.CookingTime,
                Servings = r.Servings,
                Tags = r.Tags,
                Notes = r.Notes,
                CreatedDate = r.CreatedDate,
                ModifiedDate = r.ModifiedDate,
                ImageContentType = r.ImageContentType,
                RecipeIngredients = r.RecipeIngredients,
                // Exclude ImageData for performance
                ImageData = null
            })
            .OrderByDescending(r => r.ModifiedDate)
            .ToListAsync();
    }

    public async Task<List<string>> GetAllTagsAsync()
    {
        var allTags = await _context.Recipes
            .Where(r => !string.IsNullOrWhiteSpace(r.Tags))
            .Select(r => r.Tags)
            .ToListAsync();

        var uniqueTags = allTags
            .SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t)
            .ToList();

        return uniqueTags;
    }

    public async Task<List<Recipe>> FilterRecipesByIngredientsAsync(List<int> ingredientIds)
    {
        if (ingredientIds == null || !ingredientIds.Any())
        {
            return await GetAllRecipesAsync();
        }

        // Find recipes that contain ALL of the selected ingredients
        var query = _context.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .AsQueryable();

        // Filter to only recipes that have all selected ingredients
        foreach (var ingredientId in ingredientIds)
        {
            query = query.Where(r => r.RecipeIngredients.Any(ri => ri.IngredientId == ingredientId));
        }

        return await query
            .Select(r => new Recipe
            {
                Id = r.Id,
                Name = r.Name,
                Instructions = r.Instructions,
                CookingTime = r.CookingTime,
                Servings = r.Servings,
                Tags = r.Tags,
                Notes = r.Notes,
                CreatedDate = r.CreatedDate,
                ModifiedDate = r.ModifiedDate,
                ImageContentType = r.ImageContentType,
                RecipeIngredients = r.RecipeIngredients,
                // Exclude ImageData for performance
                ImageData = null
            })
            .OrderByDescending(r => r.ModifiedDate)
            .ToListAsync();
    }

    public async Task<List<Recipe>> FilterRecipesByIngredientsWithFiltersAsync(
        List<int> ingredientIds,
        string? searchTerm = null,
        int? minCookTime = null,
        int? maxCookTime = null,
        List<string>? tags = null,
        int? minServings = null,
        int? maxServings = null)
    {
        var query = _context.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .AsQueryable();

        // Filter by ingredients - recipes must contain ALL selected ingredients
        if (ingredientIds != null && ingredientIds.Any())
        {
            foreach (var ingredientId in ingredientIds)
            {
                query = query.Where(r => r.RecipeIngredients.Any(ri => ri.IngredientId == ingredientId));
            }
        }

        // Apply search term filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearchTerm = searchTerm.ToLower();
            query = query.Where(r => r.Name.ToLower().Contains(lowerSearchTerm) ||
                                   r.RecipeIngredients.Any(ri => ri.Ingredient.Name.ToLower().Contains(lowerSearchTerm)) ||
                                   r.Tags.ToLower().Contains(lowerSearchTerm));
        }

        // Apply cook time filters
        if (minCookTime.HasValue)
        {
            query = query.Where(r => r.CookingTime >= minCookTime.Value);
        }
        if (maxCookTime.HasValue)
        {
            query = query.Where(r => r.CookingTime <= maxCookTime.Value);
        }

        // Apply serving count filters
        if (minServings.HasValue)
        {
            query = query.Where(r => r.Servings >= minServings.Value);
        }
        if (maxServings.HasValue)
        {
            query = query.Where(r => r.Servings <= maxServings.Value);
        }

        // Apply tag filters (matches if recipe contains any of the selected tags)
        if (tags != null && tags.Any())
        {
            var lowerTags = tags.Select(t => t.ToLower().Trim()).ToList();
            query = query.Where(r => !string.IsNullOrWhiteSpace(r.Tags) && 
                lowerTags.Any(tag => 
                    r.Tags.ToLower().Contains("," + tag + ",") ||
                    r.Tags.ToLower().StartsWith(tag + ",") ||
                    r.Tags.ToLower().EndsWith("," + tag) ||
                    r.Tags.ToLower() == tag));
        }

        return await query
            .Select(r => new Recipe
            {
                Id = r.Id,
                Name = r.Name,
                Instructions = r.Instructions,
                CookingTime = r.CookingTime,
                Servings = r.Servings,
                Tags = r.Tags,
                Notes = r.Notes,
                CreatedDate = r.CreatedDate,
                ModifiedDate = r.ModifiedDate,
                ImageContentType = r.ImageContentType,
                RecipeIngredients = r.RecipeIngredients,
                // Exclude ImageData for performance
                ImageData = null
            })
            .OrderByDescending(r => r.ModifiedDate)
            .ToListAsync();
    }

    private async Task ProcessIngredientsAsync(Recipe recipe, List<IngredientEntry> ingredientEntries)
    {
        foreach (var entry in ingredientEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            var normalizedName = IngredientService.NormalizeName(entry.Name);

            // Find or create ingredient
            var ingredient = await _context.Ingredients
                .FirstOrDefaultAsync(i => i.Name.ToLower() == normalizedName.ToLower());

            if (ingredient == null)
            {
                ingredient = new Ingredient
                {
                    Name = normalizedName,
                    DefaultUnit = entry.Unit
                };
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
            var modifier = string.IsNullOrWhiteSpace(entry.Modifier) ? null : entry.Modifier.Trim();
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                Recipe = recipe,
                Ingredient = ingredient,
                Quantity = baseQuantity,
                Unit = baseUnit,
                Modifier = modifier
            });
        }
    }

    // Get image data separately for performance
    public async Task<(byte[]? imageData, string? contentType)> GetRecipeImageAsync(int recipeId)
    {
        var recipe = await _context.Recipes
            .Where(r => r.Id == recipeId)
            .Select(r => new { r.ImageData, r.ImageContentType })
            .FirstOrDefaultAsync();
            
        return recipe != null ? (recipe.ImageData, recipe.ImageContentType) : (null, null);
    }

    // Check if recipe has an image
    public async Task<bool> RecipeHasImageAsync(int recipeId)
    {
        return await _context.Recipes
            .Where(r => r.Id == recipeId)
            .Select(r => r.ImageData != null)
            .FirstOrDefaultAsync();
    }

    // Maximum image size: 10MB
    public const int MaxImageSizeBytes = 10 * 1024 * 1024;
}

