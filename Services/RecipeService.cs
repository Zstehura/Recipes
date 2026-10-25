using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RecipesApp.Data;
using RecipesApp.Models;

namespace RecipesApp.Services;

public class RecipeService : IRecipeReader
{
    private readonly RecipeDbContext _context;
    private readonly ILogger<RecipeService> _logger;

    public RecipeService(RecipeDbContext context, ILogger<RecipeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Every list and detail query returns all recipe columns except the image bytes.
    // Images can be several MB, so they are served separately by /api/recipe/{id}/image.
    private static readonly Expression<Func<Recipe, Recipe>> WithoutImageData = r => new Recipe
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
        ImageData = null
    };

    private IQueryable<Recipe> RecipesWithIngredients() =>
        _context.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient);

    public async Task<List<Recipe>> GetAllRecipesAsync()
    {
        return await RecipesWithIngredients()
            .Select(WithoutImageData)
            .OrderByDescending(r => r.ModifiedDate)
            .ToListAsync();
    }

    public async Task<Recipe?> GetRecipeByIdAsync(int id)
    {
        return await RecipesWithIngredients()
            .Select(WithoutImageData)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    /// <exception cref="ValidationException">The recipe, an ingredient, or the image is invalid.</exception>
    public async Task<Recipe> AddRecipeAsync(Recipe recipe, List<IngredientEntry> ingredientEntries)
    {
        RecipeValidator.Validate(recipe, ingredientEntries);
        ValidateImage(recipe);

        recipe.CreatedDate = DateTime.Now;
        recipe.ModifiedDate = DateTime.Now;

        // Clear any existing recipe ingredients
        recipe.RecipeIngredients.Clear();

        // Process ingredients
        await ProcessIngredientsAsync(recipe, ingredientEntries);

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        // Log IDs and counts only; recipe content is personal data
        _logger.LogInformation("Created recipe {RecipeId} with {IngredientCount} ingredient(s)",
            recipe.Id, recipe.RecipeIngredients.Count);
        return recipe;
    }

    /// <exception cref="ValidationException">The recipe, an ingredient, or the image is invalid.</exception>
    public async Task<Recipe?> UpdateRecipeAsync(Recipe recipe, List<IngredientEntry> ingredientEntries, bool removeImage = false)
    {
        RecipeValidator.Validate(recipe, ingredientEntries);
        if (!removeImage)
        {
            ValidateImage(recipe);
        }

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

        _logger.LogInformation("Updated recipe {RecipeId} ({IngredientCount} ingredient(s), image removed: {ImageRemoved})",
            existingRecipe.Id, existingRecipe.RecipeIngredients.Count, removeImage);
        return existingRecipe;
    }

    public async Task<bool> DeleteRecipeAsync(int id)
    {
        var recipe = await _context.Recipes.FindAsync(id);
        if (recipe == null)
            return false;

        _context.Recipes.Remove(recipe);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted recipe {RecipeId}", id);
        return true;
    }

    public async Task<List<Recipe>> SearchRecipesAsync(string searchTerm)
    {
        return await FilterRecipesByIngredientsWithFiltersAsync(new List<int>(), searchTerm);
    }

    public async Task<List<Recipe>> FilterRecipesAsync(
        string? searchTerm = null,
        int? minCookTime = null,
        int? maxCookTime = null,
        List<string>? tags = null,
        int? minServings = null,
        int? maxServings = null)
    {
        return await FilterRecipesByIngredientsWithFiltersAsync(
            new List<int>(), searchTerm, minCookTime, maxCookTime, tags, minServings, maxServings);
    }

    public async Task<List<string>> GetAllTagsAsync()
    {
        var allTags = await _context.Recipes
            .Where(r => !string.IsNullOrWhiteSpace(r.Tags))
            .Select(r => r.Tags)
            .ToListAsync();

        var uniqueTags = allTags
            .SelectMany(RecipeTags.Parse)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t)
            .ToList();

        return uniqueTags;
    }

    public async Task<List<Recipe>> FilterRecipesByIngredientsAsync(List<int> ingredientIds)
    {
        return await FilterRecipesByIngredientsWithFiltersAsync(ingredientIds);
    }

    /// <summary>
    /// Returns recipes matching every given filter, most recently modified first.
    /// Null or empty filters are ignored, so calling it with no filters returns all recipes.
    /// </summary>
    public async Task<List<Recipe>> FilterRecipesByIngredientsWithFiltersAsync(
        List<int> ingredientIds,
        string? searchTerm = null,
        int? minCookTime = null,
        int? maxCookTime = null,
        List<string>? tags = null,
        int? minServings = null,
        int? maxServings = null)
    {
        var query = RecipesWithIngredients();

        // Filter by ingredients - recipes must contain ALL selected ingredients
        if (ingredientIds != null && ingredientIds.Any())
        {
            foreach (var ingredientId in ingredientIds)
            {
                query = query.Where(r => r.RecipeIngredients.Any(ri => ri.IngredientId == ingredientId));
            }
        }

        // Search term matches the recipe name, any ingredient name, or the tags
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

        var recipes = await query
            .Select(WithoutImageData)
            .OrderByDescending(r => r.ModifiedDate)
            .ToListAsync();

        // Tag filter (matches if the recipe has any of the selected tags). Tags are one
        // comma-separated column, so they are matched in memory, after the database has
        // applied the other filters. Fine for a personal collection; a large catalog would
        // store tags in their own table instead.
        if (tags != null && tags.Any())
        {
            recipes = recipes.Where(r => RecipeTags.ContainsAny(r.Tags, tags)).ToList();
        }

        return recipes;
    }

    private async Task ProcessIngredientsAsync(Recipe recipe, List<IngredientEntry> ingredientEntries)
    {
        foreach (var entry in ingredientEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            var normalizedName = IngredientService.NormalizeName(entry.Name);

            // Find or create ingredient. Check entities added earlier in this same save first
            // (Local), since a recipe can list the same new ingredient twice and the database
            // doesn't contain it yet.
            var ingredient = _context.Ingredients.Local
                                 .FirstOrDefault(i => string.Equals(i.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
                             ?? await _context.Ingredients
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

            // Add recipe ingredient relationship
            var modifier = string.IsNullOrWhiteSpace(entry.Modifier) ? null : entry.Modifier.Trim();
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                Recipe = recipe,
                Ingredient = ingredient,
                // Kept as entered; the grocery list converts when it adds amounts up
                Quantity = entry.Quantity,
                Unit = entry.Quantity.HasValue ? entry.Unit : null,
                Modifier = modifier
            });
        }
    }

    /// <summary>
    /// Defense in depth: the form validates uploads too, but this is the last stop before the
    /// database, so check the bytes again and set the content type from them rather than
    /// trusting whatever the caller passed.
    /// </summary>
    /// <exception cref="ValidationException">The image isn't an allowed format or is too large.</exception>
    private void ValidateImage(Recipe recipe)
    {
        if (recipe.ImageData == null)
        {
            recipe.ImageContentType = null;
            return;
        }

        var result = ImageValidator.Validate(recipe.ImageData);
        if (!result.IsValid)
        {
            _logger.LogWarning("Rejected image for recipe {RecipeId}: {SizeBytes} bytes, not an allowed format or too large",
                recipe.Id, recipe.ImageData.Length);
            throw new ValidationException(result.ErrorMessage);
        }

        recipe.ImageContentType = result.ContentType;
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
}

