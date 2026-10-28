using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recipes.Tests.TestData;
using RecipesApp.Models;
using RecipesApp.Services;
using Shouldly;

namespace Recipes.Tests.Data;

public sealed class RecipeServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private RecipeService CreateService() => new(_db.CreateContext(), NullLogger<RecipeService>.Instance);

    private static Recipe NewRecipe(string name = "Pancakes") => new()
    {
        Name = name,
        Instructions = "Mix and cook.",
        CookingTime = 20,
        Servings = 4,
        Tags = "Breakfast",
        Notes = "",
    };

    private static IngredientEntry Entry(string name, decimal? quantity, MeasurementUnit unit, string modifier = "") =>
        new() { Name = name, Quantity = quantity, Unit = unit, Modifier = modifier };

    private async Task<Recipe> LoadRecipe(int id)
    {
        using var context = _db.CreateContext();
        return await context.Recipes
            .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.Ingredient)
            .SingleAsync(r => r.Id == id);
    }

    // ---------------------------------------------------------------- Add

    [Fact]
    public async Task AddRecipeAsync_NewIngredients_CreatesNormalizedIngredients()
    {
        var recipe = await CreateService().AddRecipeAsync(NewRecipe(), new List<IngredientEntry>
        {
            Entry("  flour ", 250, MeasurementUnit.Grams),
            Entry("eggs", 2, MeasurementUnit.Pieces),
        });

        var saved = await LoadRecipe(recipe.Id);
        saved.RecipeIngredients.Select(ri => ri.Ingredient.Name).ShouldBe(new[] { "Flour", "Eggs" }, ignoreOrder: true);
    }

    [Theory]
    [InlineData(MeasurementUnit.Cups, 2)]
    [InlineData(MeasurementUnit.Kilograms, 1.5)]
    [InlineData(MeasurementUnit.Teaspoons, 0.5)]
    [InlineData(MeasurementUnit.FluidOunces, 8)]
    [InlineData(MeasurementUnit.Pieces, 3)]
    public async Task AddRecipeAsync_IngredientQuantity_IsStoredAsEntered(MeasurementUnit unit, double quantity)
    {
        var recipe = await CreateService().AddRecipeAsync(NewRecipe(), new List<IngredientEntry>
        {
            Entry("Milk", (decimal)quantity, unit),
        });

        var link = (await LoadRecipe(recipe.Id)).RecipeIngredients.ShouldHaveSingleItem();
        link.Quantity.ShouldBe((decimal)quantity);
        link.Unit.ShouldBe(unit);
    }

    [Fact]
    public async Task UnitsAreStoredByName_InTheTextColumn()
    {
        await CreateService().AddRecipeAsync(NewRecipe(), new List<IngredientEntry> { Entry("Milk", 2m, MeasurementUnit.Tablespoons) });

        using var context = _db.CreateContext();
        context.Database.SqlQueryRaw<string>("SELECT Unit AS Value FROM RecipeIngredients").Single().ShouldBe("Tablespoons");
    }

    [Fact]
    public async Task AddRecipeAsync_NoQuantity_StoresNullQuantityAndUnit()
    {
        var recipe = await CreateService().AddRecipeAsync(NewRecipe(), new List<IngredientEntry>
        {
            Entry("Salt", null, MeasurementUnit.Pieces),
        });

        var link = (await LoadRecipe(recipe.Id)).RecipeIngredients.ShouldHaveSingleItem();
        link.Quantity.ShouldBeNull();
        link.Unit.ShouldBeNull();
    }

    [Theory]
    [InlineData("  diced ", "diced")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public async Task AddRecipeAsync_Modifier_IsTrimmedOrNull(string modifier, string? expected)
    {
        var recipe = await CreateService().AddRecipeAsync(NewRecipe(), new List<IngredientEntry>
        {
            Entry("Tomato", 1, MeasurementUnit.Pieces, modifier),
        });

        (await LoadRecipe(recipe.Id)).RecipeIngredients.ShouldHaveSingleItem().Modifier.ShouldBe(expected);
    }

    [Fact]
    public async Task AddRecipeAsync_ExistingIngredientDifferentCase_ReusesIt()
    {
        var flourId = _db.SeedIngredient("Flour", MeasurementUnit.Grams);

        var recipe = await CreateService().AddRecipeAsync(NewRecipe(), new List<IngredientEntry>
        {
            Entry("FLOUR", 1, MeasurementUnit.Cups),
        });

        (await LoadRecipe(recipe.Id)).RecipeIngredients.ShouldHaveSingleItem().IngredientId.ShouldBe(flourId);
        using var context = _db.CreateContext();
        var flour = await context.Ingredients.SingleAsync();
        flour.Name.ShouldBe("Flour");
        flour.DefaultUnit.ShouldBe(MeasurementUnit.Grams, "an existing ingredient's default unit is not changed");
    }

    [Fact]
    public async Task AddRecipeAsync_NewIngredient_UsesEntryUnitAsDefaultUnit()
    {
        await CreateService().AddRecipeAsync(NewRecipe(), new List<IngredientEntry>
        {
            Entry("Milk", 1, MeasurementUnit.Cups),
        });

        using var context = _db.CreateContext();
        (await context.Ingredients.SingleAsync()).DefaultUnit.ShouldBe(MeasurementUnit.Cups);
    }

    [Fact]
    public async Task AddRecipeAsync_BlankIngredientNames_AreSkipped()
    {
        var recipe = await CreateService().AddRecipeAsync(NewRecipe(), new List<IngredientEntry>
        {
            Entry("Flour", 1, MeasurementUnit.Cups),
            Entry("", 1, MeasurementUnit.Cups),
            Entry("   ", 1, MeasurementUnit.Cups),
        });

        (await LoadRecipe(recipe.Id)).RecipeIngredients.ShouldHaveSingleItem().Ingredient.Name.ShouldBe("Flour");
    }

    [Fact]
    public async Task AddRecipeAsync_SetsCreatedAndModifiedDatesToNow()
    {
        var before = DateTime.Now;
        var input = NewRecipe();
        input.CreatedDate = new DateTime(2000, 1, 1);

        var recipe = await CreateService().AddRecipeAsync(input, new List<IngredientEntry>());

        var saved = await LoadRecipe(recipe.Id);
        saved.CreatedDate.ShouldBeGreaterThanOrEqualTo(before);
        saved.ModifiedDate.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public async Task AddRecipeAsync_SameIngredientWithDifferentModifiers_SavesBoth()
    {
        var recipe = await CreateService().AddRecipeAsync(NewRecipe(), new List<IngredientEntry>
        {
            Entry("Tomato", 2, MeasurementUnit.Pieces, "sliced"),
            Entry("Tomato", 1, MeasurementUnit.Pieces, "diced"),
        });

        (await LoadRecipe(recipe.Id)).RecipeIngredients.Select(ri => ri.Modifier)
            .ShouldBe(new[] { "sliced", "diced" }, ignoreOrder: true);
    }

    [Fact]
    public async Task AddRecipeAsync_SameNewIngredientTwice_CreatesOneIngredientAndTwoLines()
    {
        var recipe = await CreateService().AddRecipeAsync(NewRecipe(), new List<IngredientEntry>
        {
            Entry("Butter", 50, MeasurementUnit.Grams),
            Entry("butter", 25, MeasurementUnit.Grams, "melted"),
        });

        var saved = await LoadRecipe(recipe.Id);
        saved.RecipeIngredients.Count.ShouldBe(2);
        saved.RecipeIngredients.Select(ri => ri.IngredientId).Distinct().ShouldHaveSingleItem();
        using var context = _db.CreateContext();
        (await context.Ingredients.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task UpdateRecipeAsync_SameIngredientTwice_SavesBothLines()
    {
        var id = _db.SeedRecipe("Salad", "Tomato");
        var input = NewRecipe("Salad");
        input.Id = id;

        await CreateService().UpdateRecipeAsync(input, new List<IngredientEntry>
        {
            Entry("Tomato", 2, MeasurementUnit.Pieces, "sliced"),
            Entry("Tomato", 1, MeasurementUnit.Pieces, "diced"),
        });

        (await LoadRecipe(id)).RecipeIngredients.Select(ri => ri.Modifier)
            .ShouldBe(new[] { "sliced", "diced" }, ignoreOrder: true);
    }

    [Fact]
    public async Task ImportFlow_ParseMapAndSave_StoresQuantitiesAsWritten()
    {
        // The same steps the Import page runs: parse text, map lines to entries, save.
        var text = Recipes.Tests.TestData.RecipeTextBuilder.Build(ingredients: new[]
        {
            "2 cups milk", "1.5 kg flour", "salt (to taste)",
        });
        var parsed = new RecipeImportExportService().ParseRecipeText(text).recipes.ShouldHaveSingleItem();
        var entries = IngredientEntryMapper.FromRecipeIngredients(parsed.RecipeIngredients);
        parsed.RecipeIngredients = new List<RecipeIngredient>();

        var saved = await CreateService().AddRecipeAsync(parsed, entries);

        var lines = (await LoadRecipe(saved.Id)).RecipeIngredients.ToDictionary(ri => ri.Ingredient.Name);
        lines["Milk"].Quantity.ShouldBe(2m);
        lines["Milk"].Unit.ShouldBe(MeasurementUnit.Cups);
        lines["Flour"].Quantity.ShouldBe(1.5m);
        lines["Flour"].Unit.ShouldBe(MeasurementUnit.Kilograms);
        lines["Salt"].Quantity.ShouldBeNull();
    }

    // ---------------------------------------------------------------- Read

    [Fact]
    public async Task GetRecipeByIdAsync_ExistingRecipe_ReturnsIngredientsButNotImageData()
    {
        var id = _db.SeedRecipe("Bread", new[] { ("Flour", (decimal?)500m, (string?)"g", (string?)null) },
            image: new byte[] { 1, 2, 3 });

        var recipe = await CreateService().GetRecipeByIdAsync(id);

        recipe.ShouldNotBeNull();
        recipe.Name.ShouldBe("Bread");
        recipe.ImageData.ShouldBeNull("image bytes are served separately for performance");
        recipe.ImageContentType.ShouldBe("image/png");
        var link = recipe.RecipeIngredients.ShouldHaveSingleItem();
        link.Ingredient.Name.ShouldBe("Flour");
        link.Quantity.ShouldBe(500m);
    }

    [Fact]
    public async Task GetRecipeByIdAsync_UnknownId_ReturnsNull()
    {
        (await CreateService().GetRecipeByIdAsync(999)).ShouldBeNull();
    }

    [Fact]
    public async Task GetAllRecipesAsync_ReturnsMostRecentlyModifiedFirst()
    {
        _db.SeedRecipe("Old", modified: new DateTime(2024, 1, 1));
        _db.SeedRecipe("Newest", modified: new DateTime(2024, 3, 1));
        _db.SeedRecipe("Middle", modified: new DateTime(2024, 2, 1));

        var recipes = await CreateService().GetAllRecipesAsync();

        recipes.Select(r => r.Name).ShouldBe(new[] { "Newest", "Middle", "Old" });
    }

    [Fact]
    public async Task GetAllRecipesAsync_EmptyDatabase_ReturnsEmptyList()
    {
        (await CreateService().GetAllRecipesAsync()).ShouldBeEmpty();
    }

    // ---------------------------------------------------------------- Update

    [Fact]
    public async Task UpdateRecipeAsync_ExistingRecipe_UpdatesFieldsAndReplacesIngredients()
    {
        var created = new DateTime(2024, 1, 1);
        var id = _db.SeedRecipe("Bread", new[] { ("Flour", (decimal?)500m, (string?)"g", (string?)null) }, modified: created);
        var before = DateTime.Now;

        var result = await CreateService().UpdateRecipeAsync(
            new Recipe { Id = id, Name = "Sourdough", Instructions = "Wait.", CookingTime = 60, Servings = 2, Tags = "Bread", Notes = "Tangy" },
            new List<IngredientEntry> { Entry("Starter", 100, MeasurementUnit.Grams) });

        result.ShouldNotBeNull();
        var saved = await LoadRecipe(id);
        saved.Name.ShouldBe("Sourdough");
        saved.Instructions.ShouldBe("Wait.");
        saved.CookingTime.ShouldBe(60);
        saved.Servings.ShouldBe(2);
        saved.Tags.ShouldBe("Bread");
        saved.Notes.ShouldBe("Tangy");
        saved.CreatedDate.ShouldBe(created);
        saved.ModifiedDate.ShouldBeGreaterThanOrEqualTo(before);
        saved.RecipeIngredients.ShouldHaveSingleItem().Ingredient.Name.ShouldBe("Starter");
    }

    [Fact]
    public async Task UpdateRecipeAsync_UnknownId_ReturnsNull()
    {
        var input = NewRecipe();
        input.Id = 999;

        (await CreateService().UpdateRecipeAsync(input, new List<IngredientEntry>())).ShouldBeNull();
    }

    [Fact]
    public async Task UpdateRecipeAsync_NoNewImage_KeepsExistingImage()
    {
        var id = _db.SeedRecipe("Bread", image: new byte[] { 1, 2, 3 });
        var input = NewRecipe();
        input.Id = id;

        await CreateService().UpdateRecipeAsync(input, new List<IngredientEntry>());

        (await LoadRecipe(id)).ImageData.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task UpdateRecipeAsync_NewImage_ReplacesImage()
    {
        var id = _db.SeedRecipe("Bread", image: new byte[] { 1, 2, 3 });
        var input = NewRecipe();
        input.Id = id;
        input.ImageData = TestImages.Jpeg;
        input.ImageContentType = "text/html";   // whatever the caller claims is ignored

        await CreateService().UpdateRecipeAsync(input, new List<IngredientEntry>());

        var saved = await LoadRecipe(id);
        saved.ImageData.ShouldBe(TestImages.Jpeg);
        saved.ImageContentType.ShouldBe("image/jpeg");
    }

    [Fact]
    public async Task UpdateRecipeAsync_RemoveImage_ClearsImage()
    {
        var id = _db.SeedRecipe("Bread", image: new byte[] { 1, 2, 3 });
        var input = NewRecipe();
        input.Id = id;

        await CreateService().UpdateRecipeAsync(input, new List<IngredientEntry>(), removeImage: true);

        var saved = await LoadRecipe(id);
        saved.ImageData.ShouldBeNull();
        saved.ImageContentType.ShouldBeNull();
    }

    [Fact]
    public async Task AddRecipeAsync_ValidImage_StoresContentTypeDetectedFromBytes()
    {
        var input = NewRecipe();
        input.ImageData = TestImages.Png;
        input.ImageContentType = "image/svg+xml";

        var recipe = await CreateService().AddRecipeAsync(input, new List<IngredientEntry>());

        (await LoadRecipe(recipe.Id)).ImageContentType.ShouldBe("image/png");
    }

    [Fact]
    public async Task AddRecipeAsync_SvgImage_ThrowsAndSavesNothing()
    {
        var input = NewRecipe();
        input.ImageData = TestImages.Svg;
        input.ImageContentType = "image/svg+xml";

        await Should.ThrowAsync<ValidationException>(() => CreateService().AddRecipeAsync(input, new List<IngredientEntry>()));

        using var context = _db.CreateContext();
        (await context.Recipes.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateRecipeAsync_InvalidNewImage_ThrowsAndKeepsExistingImage()
    {
        var id = _db.SeedRecipe("Bread", image: TestImages.Png);
        var input = NewRecipe();
        input.Id = id;
        input.ImageData = TestImages.Html;

        await Should.ThrowAsync<ValidationException>(() => CreateService().UpdateRecipeAsync(input, new List<IngredientEntry>()));

        var saved = await LoadRecipe(id);
        saved.ImageData.ShouldBe(TestImages.Png);
        saved.Name.ShouldBe("Bread");
    }

    [Fact]
    public async Task AddRecipeAsync_InvalidIngredient_ThrowsAndSavesNothing()
    {
        var entries = new List<IngredientEntry> { Entry("Flour", -5, MeasurementUnit.Grams) };

        await Should.ThrowAsync<ValidationException>(() => CreateService().AddRecipeAsync(NewRecipe(), entries));

        using var context = _db.CreateContext();
        (await context.Recipes.AnyAsync()).ShouldBeFalse();
        (await context.Ingredients.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateRecipeAsync_InvalidRecipe_ThrowsAndKeepsExistingData()
    {
        var id = _db.SeedRecipe("Bread", "Flour");
        var input = NewRecipe();
        input.Id = id;
        input.Name = new string('x', 201);

        await Should.ThrowAsync<ValidationException>(() => CreateService().UpdateRecipeAsync(input, new List<IngredientEntry>()));

        var saved = await LoadRecipe(id);
        saved.Name.ShouldBe("Bread");
        saved.RecipeIngredients.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ImportFlow_OversizedIngredientName_IsRejectedOnSave()
    {
        // The parser accepts any line; RecipeService enforces the limits for every save path.
        var text = Recipes.Tests.TestData.RecipeTextBuilder.Build(ingredients: new[] { "2 " + new string('x', 300) });
        var parsed = new RecipeImportExportService().ParseRecipeText(text).recipes.ShouldHaveSingleItem();
        var entries = IngredientEntryMapper.FromRecipeIngredients(parsed.RecipeIngredients);
        parsed.RecipeIngredients = new List<RecipeIngredient>();

        var ex = await Should.ThrowAsync<ValidationException>(() => CreateService().AddRecipeAsync(parsed, entries));

        ex.Message.ShouldContain("Name cannot exceed 200 characters");
    }

    [Fact]
    public async Task AddRecipeAsync_LogsRecipeIdButNotPersonalContent()
    {
        var logger = new ListLogger<RecipeService>();
        var service = new RecipeService(_db.CreateContext(), logger);
        var input = NewRecipe("Grandma's Secret Lasagna");

        var recipe = await service.AddRecipeAsync(input, new List<IngredientEntry> { Entry("Secret spice", 1, MeasurementUnit.Grams) });

        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Message.ShouldBe($"Created recipe {recipe.Id} with 1 ingredient(s)");
        logger.Entries.ShouldAllBe(e => !e.Message.Contains("Lasagna") && !e.Message.Contains("Secret spice"));
    }

    [Fact]
    public async Task AddRecipeAsync_RejectedImage_LogsWarningWithoutContent()
    {
        var logger = new ListLogger<RecipeService>();
        var service = new RecipeService(_db.CreateContext(), logger);
        var input = NewRecipe("Private Recipe");
        input.ImageData = TestImages.Svg;

        await Should.ThrowAsync<ValidationException>(() => service.AddRecipeAsync(input, new List<IngredientEntry>()));

        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Warning);
        entry.Message.ShouldNotContain("Private Recipe");
        entry.Message.ShouldNotContain("<script>");
    }

    // ---------------------------------------------------------------- Delete

    [Fact]
    public async Task DeleteRecipeAsync_ExistingRecipe_DeletesRecipeAndLinksButKeepsIngredients()
    {
        var id = _db.SeedRecipe("Bread", "Flour", "Water");

        var result = await CreateService().DeleteRecipeAsync(id);

        result.ShouldBeTrue();
        using var context = _db.CreateContext();
        (await context.Recipes.AnyAsync()).ShouldBeFalse();
        (await context.RecipeIngredients.AnyAsync()).ShouldBeFalse();
        (await context.Ingredients.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task DeleteRecipeAsync_UnknownId_ReturnsFalse()
    {
        (await CreateService().DeleteRecipeAsync(999)).ShouldBeFalse();
    }

    // ---------------------------------------------------------------- Images

    [Fact]
    public async Task GetRecipeImageAsync_RecipeWithImage_ReturnsBytesAndContentType()
    {
        var id = _db.SeedRecipe("Bread", image: new byte[] { 1, 2, 3 });

        var (data, contentType) = await CreateService().GetRecipeImageAsync(id);

        data.ShouldBe(new byte[] { 1, 2, 3 });
        contentType.ShouldBe("image/png");
    }

    [Fact]
    public async Task GetRecipeImageAsync_UnknownId_ReturnsNulls()
    {
        var (data, contentType) = await CreateService().GetRecipeImageAsync(999);

        data.ShouldBeNull();
        contentType.ShouldBeNull();
    }

    [Fact]
    public async Task RecipeHasImageAsync_ReflectsWhetherImageIsStored()
    {
        var withImage = _db.SeedRecipe("Bread", image: new byte[] { 1 });
        var withoutImage = _db.SeedRecipe("Soup");
        var service = CreateService();

        (await service.RecipeHasImageAsync(withImage)).ShouldBeTrue();
        (await service.RecipeHasImageAsync(withoutImage)).ShouldBeFalse();
        (await service.RecipeHasImageAsync(999)).ShouldBeFalse();
    }

    // ---------------------------------------------------------------- Search and filter

    /// <summary>Seeds a small, varied catalog used by the search and filter tests.</summary>
    private void SeedCatalog()
    {
        _db.SeedRecipe("Spaghetti", Pieces("Pasta", "Tomato"), cookingTime: 30, servings: 4, tags: "Italian,Dinner");
        _db.SeedRecipe("Pancakes", Pieces("Flour", "Eggs"), cookingTime: 20, servings: 2, tags: "Breakfast,Sweet");
        _db.SeedRecipe("Lasagna", Pieces("Pasta", "Cheese"), cookingTime: 90, servings: 8, tags: "Italian");
        _db.SeedRecipe("Omelette", Pieces("Eggs", "Cheese"), cookingTime: 10, servings: 1, tags: "");
    }

    private static (string, decimal?, string?, string?)[] Pieces(params string[] names) =>
        names.Select(n => (n, (decimal?)1m, (string?)"pieces", (string?)null)).ToArray();

    private static string[] Names(IEnumerable<Recipe> recipes) => recipes.Select(r => r.Name).Order().ToArray();

    private static string[] Csv(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Order().ToArray();

    [Theory]
    [InlineData("spag", "Spaghetti")]               // name, partial, case-insensitive
    [InlineData("PASTA", "Lasagna,Spaghetti")]      // ingredient name
    [InlineData("italian", "Lasagna,Spaghetti")]    // tag
    [InlineData("eggs", "Omelette,Pancakes")]
    [InlineData("chocolate", "")]                   // no match
    public async Task SearchRecipesAsync_Term_MatchesNameIngredientOrTag(string term, string expected)
    {
        SeedCatalog();

        var results = await CreateService().SearchRecipesAsync(term);

        Names(results).ShouldBe(Csv(expected));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchRecipesAsync_BlankTerm_ReturnsAllRecipes(string? term)
    {
        SeedCatalog();

        (await CreateService().SearchRecipesAsync(term!)).Count.ShouldBe(4);
    }

    [Theory]
    [InlineData(null, null, null, null, null, null, "Lasagna,Omelette,Pancakes,Spaghetti")]
    [InlineData(null, 30, null, null, null, null, "Lasagna,Spaghetti")]
    [InlineData(null, null, 20, null, null, null, "Omelette,Pancakes")]
    [InlineData(null, 20, 30, null, null, null, "Pancakes,Spaghetti")]
    [InlineData(null, null, null, 4, null, null, "Lasagna,Spaghetti")]
    [InlineData(null, null, null, null, 2, null, "Omelette,Pancakes")]
    [InlineData(null, null, null, 2, 4, null, "Pancakes,Spaghetti")]
    [InlineData(null, null, null, null, null, "Italian", "Lasagna,Spaghetti")]
    [InlineData(null, null, null, null, null, "sweet,DINNER", "Pancakes,Spaghetti")]
    [InlineData(null, null, null, null, null, "Ital", "")]
    [InlineData("pasta", null, 60, null, null, null, "Spaghetti")]
    [InlineData("cheese", null, null, null, null, "Italian", "Lasagna")]
    [InlineData(null, 100, null, null, null, null, "")]
    public async Task FilterRecipesAsync_Criteria_ReturnsMatchingRecipes(
        string? search, int? minCook, int? maxCook, int? minServings, int? maxServings, string? tagsCsv, string expected)
    {
        SeedCatalog();
        var tags = tagsCsv?.Split(',').ToList();

        var results = await CreateService().FilterRecipesAsync(search, minCook, maxCook, tags, minServings, maxServings);

        Names(results).ShouldBe(Csv(expected));
    }

    [Fact]
    public async Task FilterRecipesAsync_TagsWithSpaceAfterComma_MatchesTag()
    {
        _db.SeedRecipe("Spaghetti", tags: "Italian, Dinner");

        var results = await CreateService().FilterRecipesAsync(tags: new List<string> { "Dinner" });

        Names(results).ShouldBe(new[] { "Spaghetti" });
    }

    [Fact]
    public async Task GetAllTagsAsync_ReturnsDistinctTrimmedSortedTags()
    {
        _db.SeedRecipe("A", tags: "Italian,Dinner");
        _db.SeedRecipe("B", tags: " italian , Quick ,");
        _db.SeedRecipe("C", tags: "");
        _db.SeedRecipe("D", tags: "Sweet");

        var tags = await CreateService().GetAllTagsAsync();

        tags.Select(t => t.ToLowerInvariant()).ShouldBe(new[] { "dinner", "italian", "quick", "sweet" });
    }

    // ---------------------------------------------------------------- Search by available ingredients

    private async Task<List<int>> IngredientIds(params string[] names)
    {
        using var context = _db.CreateContext();
        return await context.Ingredients.Where(i => names.Contains(i.Name)).Select(i => i.Id).ToListAsync();
    }

    [Theory]
    [InlineData("Pasta,Tomato", "Spaghetti")]              // recipe has exactly the selected ingredients
    [InlineData("Pasta", "Lasagna,Spaghetti")]             // recipes may have more than selected
    [InlineData("Eggs,Cheese", "Omelette")]
    [InlineData("Eggs,Pasta", "")]                         // no recipe has all of them
    public async Task FilterRecipesByIngredientsAsync_Selection_ReturnsRecipesContainingAll(string selected, string expected)
    {
        SeedCatalog();
        var ids = await IngredientIds(selected.Split(','));

        var results = await CreateService().FilterRecipesByIngredientsAsync(ids);

        Names(results).ShouldBe(Csv(expected));
    }

    [Fact]
    public async Task FilterRecipesByIngredientsAsync_NoSelection_ReturnsAllRecipes()
    {
        SeedCatalog();

        (await CreateService().FilterRecipesByIngredientsAsync(new List<int>())).Count.ShouldBe(4);
    }

    [Theory]
    [InlineData("Pasta", null, 60, null, "Spaghetti")]
    [InlineData("Cheese", null, null, "Italian", "Lasagna")]
    [InlineData("", null, 20, null, "Omelette,Pancakes")]
    [InlineData("Eggs", 15, null, null, "Pancakes")]
    public async Task FilterRecipesByIngredientsWithFiltersAsync_Combined_AppliesAllFilters(
        string selected, int? minCook, int? maxCook, string? tag, string expected)
    {
        SeedCatalog();
        var ids = selected == "" ? new List<int>() : await IngredientIds(selected.Split(','));
        var tags = tag == null ? null : new List<string> { tag };

        var results = await CreateService().FilterRecipesByIngredientsWithFiltersAsync(
            ids, minCookTime: minCook, maxCookTime: maxCook, tags: tags);

        Names(results).ShouldBe(Csv(expected));
    }

    // ---------------------------------------------------------------- Made from only these

    // The test database starts with the default staples: Salt, Black pepper, Water, Olive oil, Vegetable oil
    private void SeedPantryCatalog()
    {
        _db.SeedRecipe("Spaghetti", Pieces("Pasta", "Tomato", "Salt", "Olive oil"), cookingTime: 30);
        _db.SeedRecipe("Omelette", Pieces("Eggs", "Salt"), cookingTime: 10);
        _db.SeedRecipe("Pancakes", Pieces("Flour", "Eggs", "Milk"), cookingTime: 20);
        _db.SeedRecipe("Salted water", Pieces("Salt", "Water"), cookingTime: 5);
        _db.SeedIngredient("Bread");
    }

    private async Task<List<Recipe>> OnlyThese(string selected, int? maxCookTime = null)
    {
        var ids = await IngredientIds(selected.Split(','));
        return await CreateService().FilterRecipesByIngredientsWithFiltersAsync(
            ids, maxCookTime: maxCookTime, match: IngredientMatch.OnlyThese);
    }

    private void SetStaples(params string[] names)
    {
        using var context = _db.CreateContext();
        context.Staples.RemoveRange(context.Staples);
        context.Staples.AddRange(names.Select(n => new Staple { Name = n }));
        context.SaveChanges();
    }

    [Theory]
    [InlineData("Eggs", "Omelette")]                        // salt is a staple, so it needn't be picked
    [InlineData("Pasta,Tomato", "Spaghetti")]               // salt and olive oil are staples
    [InlineData("Pasta", "")]                               // tomato is missing and isn't a staple
    [InlineData("Eggs,Flour", "Omelette")]                  // pancakes still need milk
    [InlineData("Eggs,Flour,Milk", "Omelette,Pancakes")]    // picking more than a recipe needs is fine
    [InlineData("Salt", "Salted water")]                    // a picked staple counts as picked
    [InlineData("Bread", "")]                               // a recipe of only staples needs one picked
    public async Task OnlyThese_WithStaples_ReturnsRecipesMadeFromPickedAndStaples(string selected, string expected)
    {
        SeedPantryCatalog();

        Names(await OnlyThese(selected)).ShouldBe(Csv(expected));
    }

    [Theory]
    [InlineData("Eggs", "")]
    [InlineData("Pasta,Tomato", "")]
    [InlineData("Eggs,Salt", "Omelette")]
    [InlineData("Pasta,Tomato,Salt,Olive oil", "Spaghetti")]
    public async Task OnlyThese_NoStaples_EveryIngredientMustBePicked(string selected, string expected)
    {
        SeedPantryCatalog();
        SetStaples();

        Names(await OnlyThese(selected)).ShouldBe(Csv(expected));
    }

    [Fact]
    public async Task OnlyThese_StapleNames_MatchIngredientsWhateverTheCase()
    {
        SeedPantryCatalog();
        SetStaples("SALT");

        Names(await OnlyThese("Eggs")).ShouldBe(new[] { "Omelette" });
    }

    [Fact]
    public async Task OnlyThese_RecipeWithNoIngredients_IsNotAMatch()
    {
        SeedPantryCatalog();
        _db.SeedRecipe("Mystery", Array.Empty<string>());

        Names(await OnlyThese("Eggs,Flour,Milk,Pasta,Tomato")).ShouldBe(new[] { "Omelette", "Pancakes", "Spaghetti" });
    }

    [Fact]
    public async Task OnlyThese_WithOtherFilters_AppliesThemToo()
    {
        SeedPantryCatalog();

        Names(await OnlyThese("Eggs,Flour,Milk", maxCookTime: 15)).ShouldBe(new[] { "Omelette" });
    }

    [Fact]
    public async Task UsesAll_IgnoresStaples()
    {
        SeedPantryCatalog();
        var eggs = await IngredientIds("Eggs");

        var results = await CreateService().FilterRecipesByIngredientsWithFiltersAsync(eggs, match: IngredientMatch.UsesAll);

        Names(results).ShouldBe(new[] { "Omelette", "Pancakes" });
    }
}
