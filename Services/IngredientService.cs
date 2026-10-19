using Microsoft.EntityFrameworkCore;
using RecipesApp.Data;
using RecipesApp.Models;

namespace RecipesApp.Services;

public class IngredientService
{
    private readonly RecipeDbContext _context;
    private readonly ILogger<IngredientService> _logger;

    public IngredientService(RecipeDbContext context, ILogger<IngredientService> logger)
    {
        _context = context;
        _logger = logger;
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

        _logger.LogInformation("Renamed ingredient {IngredientId}", id);
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

    /// <summary>
    /// Merges the source ingredient into the target: every recipe line that used the source
    /// now uses the target, and the source ingredient is deleted. If a recipe already has a
    /// target line with the same unit and modifier, the quantities are added together;
    /// otherwise the line is kept as its own entry, so no quantity is lost.
    /// </summary>
    public async Task<bool> MergeIngredientsAsync(int targetId, int sourceId)
    {
        if (targetId == sourceId)
            return false;

        var target = await _context.Ingredients.FirstOrDefaultAsync(i => i.Id == targetId);
        var source = await _context.Ingredients.FirstOrDefaultAsync(i => i.Id == sourceId);
        if (target == null || source == null)
            return false;

        var sourceLines = await _context.RecipeIngredients
            .Where(ri => ri.IngredientId == sourceId)
            .ToListAsync();
        var affectedRecipeIds = sourceLines.Select(ri => ri.RecipeId).Distinct().ToList();
        var targetLines = await _context.RecipeIngredients
            .Where(ri => ri.IngredientId == targetId && affectedRecipeIds.Contains(ri.RecipeId))
            .ToListAsync();

        // Both saves succeed or neither does
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var combinedCount = 0;
        foreach (var line in sourceLines)
        {
            var sameLine = targetLines.FirstOrDefault(t =>
                t.RecipeId == line.RecipeId &&
                t.Unit == line.Unit &&
                string.Equals(t.Modifier ?? "", line.Modifier ?? "", StringComparison.OrdinalIgnoreCase));

            if (sameLine != null)
            {
                sameLine.Quantity = AddQuantities(sameLine.Quantity, line.Quantity);
                _context.RecipeIngredients.Remove(line);
                combinedCount++;
            }
            else
            {
                line.IngredientId = targetId;
            }
        }

        // Save the re-pointed lines before deleting the source, so the cascade delete on
        // the source ingredient can't remove lines that now belong to the target.
        await _context.SaveChangesAsync();

        _context.Ingredients.Remove(source);
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        _logger.LogInformation(
            "Merged ingredient {SourceId} into {TargetId}: {MovedCount} line(s) moved, {CombinedCount} combined",
            sourceId, targetId, sourceLines.Count - combinedCount, combinedCount);
        return true;
    }

    private static decimal? AddQuantities(decimal? a, decimal? b) =>
        a.HasValue || b.HasValue ? (a ?? 0) + (b ?? 0) : null;

    public async Task<bool> DeleteIngredientAsync(int id)
    {
        var ingredient = await _context.Ingredients
            .FirstOrDefaultAsync(i => i.Id == id);

        if (ingredient == null)
            return false;

        _context.Ingredients.Remove(ingredient);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted ingredient {IngredientId}", id);
        return true;
    }

    public async Task<int> GetIngredientUsageCountAsync(int id)
    {
        // Count recipes, not lines: a recipe can list the same ingredient more than once
        return await _context.RecipeIngredients
            .Where(ri => ri.IngredientId == id)
            .Select(ri => ri.RecipeId)
            .Distinct()
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
