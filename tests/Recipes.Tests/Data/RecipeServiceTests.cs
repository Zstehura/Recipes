using Microsoft.EntityFrameworkCore;
using RecipesApp.Models;
using RecipesApp.Services;
using Shouldly;

namespace Recipes.Tests.Data;

public sealed class RecipeServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private RecipeService CreateService() => new(_db.CreateContext());

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
    [InlineData(MeasurementUnit.Cups, 2, 473.176, "ml")]
    [InlineData(MeasurementUnit.Kilograms, 1.5, 1500, "g")]
    [InlineData(MeasurementUnit.Teaspoons, 0.5, 2.46446, "ml")]
    [InlineData(MeasurementUnit.Pieces, 3, 3, "pieces")]
    public async Task AddRecipeAsync_IngredientQuantity_IsStoredInBaseUnit(
        MeasurementUnit unit, double quantity, double expectedQuantity, string expectedUnit)
    {
        var recipe = await CreateService().AddRecipeAsync(NewRecipe(), new List<IngredientEntry>
        {
            Entry("Milk", (decimal)quantity, unit),
        });

        var link = (await LoadRecipe(recipe.Id)).RecipeIngredients.ShouldHaveSingleItem();
        link.Quantity.ShouldBe((decimal)expectedQuantity);
        link.Unit.ShouldBe(expectedUnit);
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

    [Fact(Skip = "Bug #1: the (RecipeId, IngredientId) key allows each ingredient only once per recipe")]
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
        input.ImageData = new byte[] { 9, 9 };
        input.ImageContentType = "image/jpeg";

        await CreateService().UpdateRecipeAsync(input, new List<IngredientEntry>());

        var saved = await LoadRecipe(id);
        saved.ImageData.ShouldBe(new byte[] { 9, 9 });
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

    [Fact(Skip = "Bug #3: tags saved with a space after the comma ('Italian, Dinner') don't match the tag filter")]
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
}
