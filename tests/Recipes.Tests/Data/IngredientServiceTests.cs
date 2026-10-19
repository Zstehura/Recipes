using Microsoft.EntityFrameworkCore;
using RecipesApp.Models;
using RecipesApp.Services;
using Shouldly;

namespace Recipes.Tests.Data;

public sealed class IngredientServiceTests : IDisposable
{
    // xUnit creates a new instance of the test class for every test,
    // so each test gets its own empty database.
    private readonly SqliteTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private IngredientService CreateService() => new(_db.CreateContext());

    // ---------------------------------------------------------------- Queries

    [Fact]
    public async Task GetAllIngredientsAsync_ReturnsIngredientsSortedByName()
    {
        _db.SeedIngredient("Sugar");
        _db.SeedIngredient("Apples");
        _db.SeedIngredient("Milk");

        var ingredients = await CreateService().GetAllIngredientsAsync();

        ingredients.Select(i => i.Name).ShouldBe(new[] { "Apples", "Milk", "Sugar" });
    }

    [Theory]
    [InlineData("Flour")]
    [InlineData("flour")]
    [InlineData("FLOUR")]
    public async Task GetIngredientByNameAsync_AnyCase_FindsIngredient(string search)
    {
        var id = _db.SeedIngredient("Flour");

        var ingredient = await CreateService().GetIngredientByNameAsync(search);

        ingredient.ShouldNotBeNull().Id.ShouldBe(id);
    }

    [Fact]
    public async Task GetIngredientByNameAsync_Unknown_ReturnsNull()
    {
        _db.SeedIngredient("Flour");

        (await CreateService().GetIngredientByNameAsync("Sugar")).ShouldBeNull();
    }

    [Fact]
    public async Task GetIngredientUsageCountAsync_CountsRecipesUsingIngredient()
    {
        _db.SeedRecipe("Bread", "Flour", "Water");
        _db.SeedRecipe("Cake", "Flour", "Sugar");
        var flourId = await IdOf("Flour");

        var count = await CreateService().GetIngredientUsageCountAsync(flourId);

        count.ShouldBe(2);
    }

    [Fact]
    public async Task GetRecipesUsingIngredientAsync_ReturnsOnlyRecipesWithIngredient()
    {
        _db.SeedRecipe("Bread", "Flour");
        _db.SeedRecipe("Salad", "Lettuce");
        var flourId = await IdOf("Flour");

        var recipes = await CreateService().GetRecipesUsingIngredientAsync(flourId);

        recipes.ShouldHaveSingleItem().Name.ShouldBe("Bread");
    }

    // ---------------------------------------------------------------- Rename

    [Fact]
    public async Task RenameIngredientAsync_NewName_SavesNormalizedName()
    {
        var id = _db.SeedIngredient("Flour");

        var result = await CreateService().RenameIngredientAsync(id, "  whole wheat flour ");

        result.ShouldBeTrue();
        (await NameOf(id)).ShouldBe("Whole wheat flour");
    }

    [Fact]
    public async Task RenameIngredientAsync_CaseOnlyChange_Succeeds()
    {
        var id = _db.SeedIngredient("Bbq sauce");

        var result = await CreateService().RenameIngredientAsync(id, "BBQ sauce");

        result.ShouldBeTrue();
        (await NameOf(id)).ShouldBe("BBQ sauce");
    }

    [Fact]
    public async Task RenameIngredientAsync_NameUsedByAnotherIngredient_ReturnsFalseAndKeepsName()
    {
        var id = _db.SeedIngredient("Flour");
        _db.SeedIngredient("Sugar");

        var result = await CreateService().RenameIngredientAsync(id, "sugar");

        result.ShouldBeFalse();
        (await NameOf(id)).ShouldBe("Flour");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenameIngredientAsync_BlankName_ReturnsFalse(string? newName)
    {
        var id = _db.SeedIngredient("Flour");

        var result = await CreateService().RenameIngredientAsync(id, newName!);

        result.ShouldBeFalse();
        (await NameOf(id)).ShouldBe("Flour");
    }

    [Fact]
    public async Task RenameIngredientAsync_UnknownId_ReturnsFalse()
    {
        (await CreateService().RenameIngredientAsync(999, "Anything")).ShouldBeFalse();
    }

    // ---------------------------------------------------------------- Default unit

    [Fact]
    public async Task ChangeDefaultUnitAsync_ExistingIngredient_SavesUnit()
    {
        var id = _db.SeedIngredient("Flour", MeasurementUnit.Pieces);

        var result = await CreateService().ChangeDefaultUnitAsync(id, MeasurementUnit.Grams);

        result.ShouldBeTrue();
        using var context = _db.CreateContext();
        (await context.Ingredients.SingleAsync(i => i.Id == id)).DefaultUnit.ShouldBe(MeasurementUnit.Grams);
    }

    [Fact]
    public async Task ChangeDefaultUnitAsync_UnknownId_ReturnsFalse()
    {
        (await CreateService().ChangeDefaultUnitAsync(999, MeasurementUnit.Grams)).ShouldBeFalse();
    }

    // ---------------------------------------------------------------- Merge

    [Fact]
    public async Task MergeIngredientsAsync_SourceUsedInRecipes_MovesReferencesAndDeletesSource()
    {
        var breadId = _db.SeedRecipe("Bread", new[] { ("Plain flour", (decimal?)500m, (string?)"g", (string?)"sifted") });
        var cakeId = _db.SeedRecipe("Cake", "Plain flour");
        var targetId = _db.SeedIngredient("Flour");
        var sourceId = await IdOf("Plain flour");

        var result = await CreateService().MergeIngredientsAsync(targetId, sourceId);

        result.ShouldBeTrue();
        using var context = _db.CreateContext();
        (await context.Ingredients.AnyAsync(i => i.Id == sourceId)).ShouldBeFalse();
        var links = await context.RecipeIngredients.Where(ri => ri.IngredientId == targetId).ToListAsync();
        links.Select(ri => ri.RecipeId).ShouldBe(new[] { breadId, cakeId }, ignoreOrder: true);

        var bread = links.Single(ri => ri.RecipeId == breadId);
        bread.Quantity.ShouldBe(500m);
        bread.Unit.ShouldBe("g");
        bread.Modifier.ShouldBe("sifted");
    }

    [Fact]
    public async Task MergeIngredientsAsync_UnusedSource_DeletesSource()
    {
        var targetId = _db.SeedIngredient("Flour");
        var sourceId = _db.SeedIngredient("Flower");

        var result = await CreateService().MergeIngredientsAsync(targetId, sourceId);

        result.ShouldBeTrue();
        using var context = _db.CreateContext();
        (await context.Ingredients.Select(i => i.Name).ToListAsync()).ShouldBe(new[] { "Flour" });
    }

    [Fact]
    public async Task MergeIngredientsAsync_RecipeHasBothIngredients_KeepsTargetEntryOnly()
    {
        // Documents current behavior: the source line is dropped, not combined (see bug #2 below).
        var recipeId = _db.SeedRecipe("Bread", new[]
        {
            ("Flour", (decimal?)500m, (string?)"g", (string?)null),
            ("Plain flour", (decimal?)200m, (string?)"g", (string?)null),
        });
        var targetId = await IdOf("Flour");
        var sourceId = await IdOf("Plain flour");

        var result = await CreateService().MergeIngredientsAsync(targetId, sourceId);

        result.ShouldBeTrue();
        using var context = _db.CreateContext();
        var link = await context.RecipeIngredients.SingleAsync(ri => ri.RecipeId == recipeId);
        link.IngredientId.ShouldBe(targetId);
        link.Quantity.ShouldBe(500m);
    }

    [Fact(Skip = "Bug #2: merging drops the source quantity when the recipe already has the target")]
    public async Task MergeIngredientsAsync_RecipeHasBothIngredientsInSameUnit_CombinesQuantities()
    {
        var recipeId = _db.SeedRecipe("Bread", new[]
        {
            ("Flour", (decimal?)500m, (string?)"g", (string?)null),
            ("Plain flour", (decimal?)200m, (string?)"g", (string?)null),
        });

        await CreateService().MergeIngredientsAsync(await IdOf("Flour"), await IdOf("Plain flour"));

        using var context = _db.CreateContext();
        (await context.RecipeIngredients.SingleAsync(ri => ri.RecipeId == recipeId)).Quantity.ShouldBe(700m);
    }

    [Fact]
    public async Task MergeIngredientsAsync_SameId_ReturnsFalse()
    {
        var id = _db.SeedIngredient("Flour");

        (await CreateService().MergeIngredientsAsync(id, id)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MergeIngredientsAsync_UnknownIngredient_ReturnsFalseAndChangesNothing(bool targetMissing)
    {
        var existingId = _db.SeedIngredient("Flour");
        var (targetId, sourceId) = targetMissing ? (999, existingId) : (existingId, 999);

        var result = await CreateService().MergeIngredientsAsync(targetId, sourceId);

        result.ShouldBeFalse();
        using var context = _db.CreateContext();
        (await context.Ingredients.CountAsync()).ShouldBe(1);
    }

    // ---------------------------------------------------------------- Delete

    [Fact]
    public async Task DeleteIngredientAsync_UnusedIngredient_DeletesIt()
    {
        var id = _db.SeedIngredient("Flour");

        var result = await CreateService().DeleteIngredientAsync(id);

        result.ShouldBeTrue();
        using var context = _db.CreateContext();
        (await context.Ingredients.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteIngredientAsync_InUse_RemovesItFromRecipesButKeepsRecipes()
    {
        // Documents current (risky) behavior: the cascade silently removes the ingredient
        // from every recipe that uses it. The UI warns with the usage count first.
        var recipeId = _db.SeedRecipe("Bread", "Flour", "Water");
        var flourId = await IdOf("Flour");

        var result = await CreateService().DeleteIngredientAsync(flourId);

        result.ShouldBeTrue();
        using var context = _db.CreateContext();
        (await context.Recipes.AnyAsync(r => r.Id == recipeId)).ShouldBeTrue();
        var remaining = await context.RecipeIngredients
            .Where(ri => ri.RecipeId == recipeId)
            .Select(ri => ri.Ingredient.Name)
            .ToListAsync();
        remaining.ShouldBe(new[] { "Water" });
    }

    [Fact]
    public async Task DeleteIngredientAsync_UnknownId_ReturnsFalse()
    {
        (await CreateService().DeleteIngredientAsync(999)).ShouldBeFalse();
    }

    // ---------------------------------------------------------------- Helpers

    private async Task<int> IdOf(string name)
    {
        using var context = _db.CreateContext();
        return (await context.Ingredients.SingleAsync(i => i.Name == name)).Id;
    }

    private async Task<string> NameOf(int id)
    {
        using var context = _db.CreateContext();
        return (await context.Ingredients.SingleAsync(i => i.Id == id)).Name;
    }
}
