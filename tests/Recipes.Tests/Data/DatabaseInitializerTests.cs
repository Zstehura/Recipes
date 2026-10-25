using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RecipesApp.Data;
using RecipesApp.Models;
using RecipesApp.Services;
using Shouldly;

namespace Recipes.Tests.Data;

/// <summary>
/// Verifies that databases created by older versions of the app are upgraded safely:
/// same schema as a brand-new database, and no data lost.
/// </summary>
public sealed class DatabaseInitializerTests : IDisposable
{
    // The RecipeIngredients table as the first release created it: composite primary key,
    // no Modifier column.
    private const string LegacyRecipeIngredientsSql = """
        DROP TABLE "RecipeIngredients";
        CREATE TABLE "RecipeIngredients" (
            "RecipeId" INTEGER NOT NULL,
            "IngredientId" INTEGER NOT NULL,
            "Quantity" TEXT NULL,
            "Unit" TEXT NULL,
            CONSTRAINT "PK_RecipeIngredients" PRIMARY KEY ("RecipeId", "IngredientId"),
            CONSTRAINT "FK_RecipeIngredients_Ingredients_IngredientId" FOREIGN KEY ("IngredientId") REFERENCES "Ingredients" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_RecipeIngredients_Recipes_RecipeId" FOREIGN KEY ("RecipeId") REFERENCES "Recipes" ("Id") ON DELETE CASCADE
        );
        CREATE INDEX "IX_RecipeIngredients_IngredientId" ON "RecipeIngredients" ("IngredientId");
        """;

    private readonly List<SqliteConnection> _connections = new();
    private readonly List<string> _files = new();

    public void Dispose()
    {
        _connections.ForEach(c => c.Dispose());
        SqliteConnection.ClearAllPools();
        foreach (var file in _files.SelectMany(f => new[] { f }.Concat(Directory.GetFiles(Path.GetDirectoryName(f)!, Path.GetFileName(f) + ".backup-*"))))
        {
            File.Delete(file);
        }
    }

    private RecipeDbContext CreateContext(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        _connections.Add(connection);
        return new RecipeDbContext(new DbContextOptionsBuilder<RecipeDbContext>().UseSqlite(connection).Options);
    }

    private RecipeDbContext CreateInMemoryContext() => CreateContext("DataSource=:memory:");

    private string NewTempDatabasePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recipes-init-test-{Guid.NewGuid():N}.db");
        _files.Add(path);
        return path;
    }

    private static Task InitializeAsync(RecipeDbContext context) =>
        DatabaseInitializer.InitializeAsync(context, NullLogger.Instance);

    /// <summary>
    /// Creates a database in the legacy format containing one recipe with two ingredients.
    /// </summary>
    private static (int RecipeId, int FlourId, int EggsId) CreateLegacyDatabase(RecipeDbContext context, bool withModifierColumn)
    {
        context.Database.EnsureCreated();
        var recipe = new Recipe { Name = "Pancakes", Instructions = "Cook.", CookingTime = 20, Servings = 2 };
        var flour = new Ingredient { Name = "Flour" };
        var eggs = new Ingredient { Name = "Eggs" };
        context.AddRange(recipe, flour, eggs);
        context.SaveChanges();

        context.Database.ExecuteSqlRaw(LegacyRecipeIngredientsSql);
        if (withModifierColumn)
        {
            context.Database.ExecuteSqlRaw("""ALTER TABLE RecipeIngredients ADD COLUMN Modifier TEXT""");
            context.Database.ExecuteSqlRaw(
                """INSERT INTO RecipeIngredients (RecipeId, IngredientId, Quantity, Unit, Modifier) VALUES ({0}, {1}, '250.0', 'g', 'sifted')""",
                recipe.Id, flour.Id);
        }
        else
        {
            context.Database.ExecuteSqlRaw(
                """INSERT INTO RecipeIngredients (RecipeId, IngredientId, Quantity, Unit) VALUES ({0}, {1}, '250.0', 'g')""",
                recipe.Id, flour.Id);
        }
        context.Database.ExecuteSqlRaw(
            """INSERT INTO RecipeIngredients (RecipeId, IngredientId, Quantity, Unit) VALUES ({0}, {1}, '2.0', 'pieces')""",
            recipe.Id, eggs.Id);

        return (recipe.Id, flour.Id, eggs.Id);
    }

    /// <summary>The table and index definitions SQLite has stored for RecipeIngredients.</summary>
    private static List<string> RecipeIngredientsSchema(RecipeDbContext context) =>
        context.Database
            .SqlQueryRaw<string>("""SELECT type || ' ' || name || ': ' || sql AS Value FROM sqlite_master WHERE tbl_name = 'RecipeIngredients' AND sql IS NOT NULL ORDER BY name""")
            .ToList();

    private static List<string> Columns(RecipeDbContext context, string table) =>
        context.Database.SqlQuery<string>($"SELECT name AS Value FROM pragma_table_info({table})").ToList();

    [Fact]
    public async Task InitializeAsync_NewDatabase_CreatesCurrentSchema()
    {
        using var context = CreateInMemoryContext();

        await InitializeAsync(context);

        Columns(context, "RecipeIngredients").ShouldBe(
            new[] { "Id", "RecipeId", "IngredientId", "Quantity", "Unit", "Modifier" });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializeAsync_LegacyDatabase_UpgradesToExactlyTheNewSchema(bool withModifierColumn)
    {
        using var fresh = CreateInMemoryContext();
        await InitializeAsync(fresh);
        using var legacy = CreateInMemoryContext();
        CreateLegacyDatabase(legacy, withModifierColumn);

        await InitializeAsync(legacy);

        RecipeIngredientsSchema(legacy).ShouldBe(RecipeIngredientsSchema(fresh));
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "sifted")]
    public async Task InitializeAsync_LegacyDatabase_PreservesEveryRecipeLine(bool withModifierColumn, string? expectedFlourModifier)
    {
        using var context = CreateInMemoryContext();
        var (recipeId, flourId, eggsId) = CreateLegacyDatabase(context, withModifierColumn);

        await InitializeAsync(context);

        var lines = await context.RecipeIngredients.AsNoTracking().OrderBy(ri => ri.IngredientId).ToListAsync();
        lines.Count.ShouldBe(2);
        lines.ShouldAllBe(ri => ri.RecipeId == recipeId);
        lines.Select(ri => ri.Id).Distinct().Count().ShouldBe(2);

        var flour = lines.Single(ri => ri.IngredientId == flourId);
        flour.Quantity.ShouldBe(250m);
        flour.Unit.ShouldBe(MeasurementUnit.Grams);   // "g" renamed by upgrade 3
        flour.Modifier.ShouldBe(expectedFlourModifier);

        var eggs = lines.Single(ri => ri.IngredientId == eggsId);
        eggs.Quantity.ShouldBe(2m);
        eggs.Unit.ShouldBe(MeasurementUnit.Pieces);
    }

    /// <summary>
    /// The format just before units were stored by name: the current schema, but every line in
    /// base units written as "g", "ml", or "pieces".
    /// </summary>
    private static int CreateBaseUnitDatabase(RecipeDbContext context, params (string Unit, string Quantity)[] lines)
    {
        context.Database.EnsureCreated();
        var recipe = new Recipe { Name = "Curry", Instructions = "Cook.", CookingTime = 20, Servings = 2 };
        context.Add(recipe);
        for (var i = 0; i < lines.Length; i++)
        {
            context.Add(new Ingredient { Name = $"Ingredient {i}" });
        }
        context.SaveChanges();

        var ingredientIds = context.Ingredients.OrderBy(i => i.Name).Select(i => i.Id).ToList();
        for (var i = 0; i < lines.Length; i++)
        {
            context.Database.ExecuteSqlRaw(
                """INSERT INTO RecipeIngredients (RecipeId, IngredientId, Quantity, Unit) VALUES ({0}, {1}, {2}, {3})""",
                recipe.Id, ingredientIds[i], lines[i].Quantity, lines[i].Unit);
        }
        return recipe.Id;
    }

    private static List<string?> StoredUnits(RecipeDbContext context) => context.Database
        .SqlQueryRaw<string?>("SELECT Unit AS Value FROM RecipeIngredients ORDER BY IngredientId").ToList();

    [Fact]
    public async Task InitializeAsync_BaseUnitText_IsRenamedToUnitNames_KeepingQuantities()
    {
        using var context = CreateInMemoryContext();
        CreateBaseUnitDatabase(context, ("g", "600.0"), ("ml", "14.7868"), ("pieces", "2.0"), ("??", "1.0"));

        await InitializeAsync(context);

        StoredUnits(context).ShouldBe(new[] { "Grams", "Milliliters", "Pieces", "Pieces" });
        var lines = await context.RecipeIngredients.AsNoTracking().OrderBy(ri => ri.IngredientId).ToListAsync();
        lines.Select(ri => ri.Quantity).ShouldBe(new decimal?[] { 600m, 14.7868m, 2m, 1m });
    }

    [Fact]
    public async Task InitializeAsync_LinesWithoutAUnit_StayWithoutOne()
    {
        using var context = CreateInMemoryContext();
        var id = CreateBaseUnitDatabase(context, ("g", "1.0"));
        var salt = new Ingredient { Name = "Salt" };
        context.Add(salt);
        context.SaveChanges();
        context.Database.ExecuteSqlRaw("INSERT INTO RecipeIngredients (RecipeId, IngredientId) VALUES ({0}, {1})", id, salt.Id);

        await InitializeAsync(context);

        StoredUnits(context).ShouldBe(new[] { "Grams", null });
    }

    [Fact]
    public async Task InitializeAsync_BaseUnitFileDatabase_WritesOneBackup_AndNotAgain()
    {
        var path = NewTempDatabasePath();
        using (var context = CreateContext($"Data Source={path}"))
        {
            CreateBaseUnitDatabase(context, ("g", "250.0"));
            await InitializeAsync(context);
            await InitializeAsync(context);
        }

        var backup = Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".backup-*").ShouldHaveSingleItem();
        using var backupContext = CreateContext($"Data Source={backup};Mode=ReadOnly");
        StoredUnits(backupContext).ShouldBe(new[] { "g" });
    }

    [Fact]
    public async Task InitializeAsync_UpgradedDatabase_AllowsSameIngredientTwice()
    {
        using var context = CreateInMemoryContext();
        var (recipeId, _, _) = CreateLegacyDatabase(context, withModifierColumn: true);
        await InitializeAsync(context);
        context.ChangeTracker.Clear();

        var updated = await new RecipeService(context, NullLogger<RecipeService>.Instance).UpdateRecipeAsync(
            new Recipe { Id = recipeId, Name = "Salad", Instructions = "Chop.", CookingTime = 5, Servings = 2 },
            new List<IngredientEntry>
            {
                new() { Name = "Tomato", Quantity = 2, Unit = MeasurementUnit.Pieces, Modifier = "sliced" },
                new() { Name = "Tomato", Quantity = 1, Unit = MeasurementUnit.Pieces, Modifier = "diced" },
            });

        updated.ShouldNotBeNull().RecipeIngredients.Count.ShouldBe(2);
    }

    [Fact]
    public async Task InitializeAsync_RunTwice_IsSafe()
    {
        using var context = CreateInMemoryContext();
        CreateLegacyDatabase(context, withModifierColumn: false);

        await InitializeAsync(context);
        var schemaAfterFirstRun = RecipeIngredientsSchema(context);
        await InitializeAsync(context);

        RecipeIngredientsSchema(context).ShouldBe(schemaAfterFirstRun);
        (await context.RecipeIngredients.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task InitializeAsync_LegacyFileDatabase_WritesBackupOfOriginalBeforeUpgrading()
    {
        var path = NewTempDatabasePath();
        using (var context = CreateContext($"Data Source={path}"))
        {
            CreateLegacyDatabase(context, withModifierColumn: true);
            await InitializeAsync(context);
            await InitializeAsync(context);   // a second startup must not create another backup
        }

        var backups = Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".backup-*");
        var backup = backups.ShouldHaveSingleItem();
        using var backupContext = CreateContext($"Data Source={backup};Mode=ReadOnly");
        Columns(backupContext, "RecipeIngredients").ShouldNotContain("Id");
        backupContext.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM RecipeIngredients").Single().ShouldBe(2);
    }

    [Fact]
    public async Task InitializeAsync_NewFileDatabase_DoesNotWriteBackup()
    {
        var path = NewTempDatabasePath();
        using (var context = CreateContext($"Data Source={path}"))
        {
            await InitializeAsync(context);
        }

        Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".backup-*").ShouldBeEmpty();
    }
}
