using Microsoft.EntityFrameworkCore;
using RecipesApp.Data;
using RecipesApp.Models;

namespace RecipesApp.Services;

public enum AddStapleResult
{
    Added,
    Empty,
    TooLong,
    AlreadyListed,
}

/// <summary>The staples list used by the "made from only these" ingredient search.</summary>
public class StapleService
{
    public const int MaxNameLength = 200;

    private readonly RecipeDbContext _context;
    private readonly ILogger<StapleService> _logger;

    public StapleService(RecipeDbContext context, ILogger<StapleService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Staple>> GetStaplesAsync()
    {
        return await _context.Staples
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    /// <summary>Adds a staple, named the way ingredients are ("olive oil" becomes "Olive oil").</summary>
    public async Task<AddStapleResult> AddStapleAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return AddStapleResult.Empty;

        var normalizedName = IngredientService.NormalizeName(name);
        if (normalizedName.Length > MaxNameLength)
            return AddStapleResult.TooLong;

        // The column compares without case, so "salt" matches "Salt"
        if (await _context.Staples.AnyAsync(s => s.Name == normalizedName))
            return AddStapleResult.AlreadyListed;

        var staple = new Staple { Name = normalizedName };
        _context.Staples.Add(staple);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Added staple {StapleId}", staple.Id);
        return AddStapleResult.Added;
    }

    public async Task<bool> RemoveStapleAsync(int id)
    {
        var staple = await _context.Staples.FirstOrDefaultAsync(s => s.Id == id);
        if (staple == null)
            return false;

        _context.Staples.Remove(staple);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Removed staple {StapleId}", id);
        return true;
    }
}
