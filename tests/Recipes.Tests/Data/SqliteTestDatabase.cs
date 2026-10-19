using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RecipesApp.Data;
using RecipesApp.Models;

namespace Recipes.Tests.Data;

/// <summary>
/// A fresh, isolated SQLite database that lives in memory for the duration of one test.
/// </summary>
/// <remarks>
/// We use real SQLite (<c>DataSource=:memory:</c>) instead of EF Core's InMemory provider
/// because SQLite is a real relational database: it enforces primary keys, unique indexes,
/// foreign keys, cascades, and collations, and EF has to translate our LINQ into actual SQL.
/// The InMemory provider does none of that, so it can hide real bugs.
///
/// An in-memory SQLite database exists only while its connection is open, so this class
/// holds one connection open and hands out new <see cref="RecipeDbContext"/> instances that
/// share it. Use separate contexts for Arrange, Act, and Assert so assertions read from the
/// database rather than from EF's change tracker.
/// </remarks>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RecipeDbContext> _options;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<RecipeDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public RecipeDbContext CreateContext() => new(_options);

    /// <summary>Inserts an ingredient directly (bypassing the services) and returns its ID.</summary>
    public int SeedIngredient(string name, MeasurementUnit defaultUnit = MeasurementUnit.Pieces)
    {
        using var context = CreateContext();
        var ingredient = new Ingredient { Name = name, DefaultUnit = defaultUnit };
        context.Ingredients.Add(ingredient);
        context.SaveChanges();
        return ingredient.Id;
    }

    /// <summary>
    /// Inserts a recipe directly (bypassing the services) and returns its ID. Ingredients are
    /// given as base-unit tuples and are created if they don't exist yet.
    /// </summary>
    public int SeedRecipe(
        string name,
        (string Name, decimal? Quantity, string? Unit, string? Modifier)[]? ingredients = null,
        int cookingTime = 30,
        int servings = 4,
        string tags = "",
        DateTime? modified = null,
        byte[]? image = null)
    {
        using var context = CreateContext();
        var recipe = new Recipe
        {
            Name = name,
            Instructions = "Cook it.",
            CookingTime = cookingTime,
            Servings = servings,
            Tags = tags,
            CreatedDate = modified ?? DateTime.Now,
            ModifiedDate = modified ?? DateTime.Now,
            ImageData = image,
            ImageContentType = image == null ? null : "image/png",
        };

        foreach (var (ingredientName, quantity, unit, modifier) in ingredients ?? [])
        {
            var ingredient = context.Ingredients.Local.FirstOrDefault(i => i.Name == ingredientName)
                             ?? context.Ingredients.FirstOrDefault(i => i.Name == ingredientName);
            if (ingredient == null)
            {
                ingredient = new Ingredient { Name = ingredientName };
                context.Ingredients.Add(ingredient);
            }
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                Ingredient = ingredient,
                Quantity = quantity,
                Unit = unit,
                Modifier = modifier,
            });
        }

        context.Recipes.Add(recipe);
        context.SaveChanges();
        return recipe.Id;
    }

    /// <summary>Shorthand for seeding a recipe whose ingredients are 1 piece each.</summary>
    public int SeedRecipe(string name, params string[] ingredientNames) =>
        SeedRecipe(name, ingredientNames.Select(n => (n, (decimal?)1m, (string?)"pieces", (string?)null)).ToArray());

    public void Dispose() => _connection.Dispose();
}
