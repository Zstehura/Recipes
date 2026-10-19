using Microsoft.Extensions.Logging.Abstractions;
using Recipes.Tests.TestData;
using RecipesApp.Models;
using RecipesApp.Services.Drafts;
using Shouldly;

namespace Recipes.Tests.Unit;

public class RecipeDraftTests
{
    private static Recipe SampleRecipe(int id = 0) => new()
    {
        Id = id,
        Name = "Lasagna",
        Instructions = "Layer and bake.",
        CookingTime = 60,
        Servings = 6,
        Tags = "Italian",
        Notes = "Rest 10 minutes.",
        ModifiedDate = new DateTime(2026, 10, 19, 18, 30, 15, 123).AddTicks(4567),
    };

    private static List<IngredientEntry> SampleEntries() => new()
    {
        new IngredientEntry { Name = "Pasta", Quantity = 500, Unit = MeasurementUnit.Grams },
        new IngredientEntry { Name = "Salt", Quantity = null, Unit = MeasurementUnit.Pieces, Modifier = "to taste" },
    };

    [Theory]
    [InlineData(0, "recipe-draft:new")]
    [InlineData(42, "recipe-draft:42")]
    public void KeyFor_NewOrExistingRecipe_UsesSeparateKeys(int id, string expected)
    {
        RecipeDraft.KeyFor(id).ShouldBe(expected);
    }

    [Fact]
    public void CaptureThenApply_CopiesEveryFormField()
    {
        var draft = RecipeDraft.Capture(SampleRecipe(), SampleEntries(), baseModifiedDate: null);
        var target = new Recipe();

        var entries = draft.ApplyTo(target);

        target.Name.ShouldBe("Lasagna");
        target.Instructions.ShouldBe("Layer and bake.");
        target.CookingTime.ShouldBe(60);
        target.Servings.ShouldBe(6);
        target.Tags.ShouldBe("Italian");
        target.Notes.ShouldBe("Rest 10 minutes.");
        entries.Select(e => (e.Name, e.Quantity, e.Unit, e.Modifier)).ShouldBe(new[]
        {
            ("Pasta", (decimal?)500m, MeasurementUnit.Grams, ""),
            ("Salt", (decimal?)null, MeasurementUnit.Pieces, "to taste"),
        });
    }

    [Fact]
    public void Capture_CopiesEntries_SoLaterFormEditsDontChangeTheDraft()
    {
        var entries = SampleEntries();
        var draft = RecipeDraft.Capture(SampleRecipe(), entries, null);

        entries[0].Name = "Changed";

        draft.Ingredients[0].Name.ShouldBe("Pasta");
    }

    [Fact]
    public void IsStaleFor_NewRecipe_IsNeverStale()
    {
        RecipeDraft.Capture(SampleRecipe(), SampleEntries(), null).IsStaleFor(new Recipe()).ShouldBeFalse();
    }

    [Fact]
    public void IsStaleFor_ExistingRecipeUnchangedSinceDraft_IsNotStale()
    {
        var recipe = SampleRecipe(id: 7);
        var draft = RecipeDraft.Capture(recipe, SampleEntries(), recipe.ModifiedDate);

        draft.IsStaleFor(recipe).ShouldBeFalse();
    }

    [Fact]
    public void IsStaleFor_ExistingRecipeSavedSinceDraft_IsStale()
    {
        var recipe = SampleRecipe(id: 7);
        var draft = RecipeDraft.Capture(recipe, SampleEntries(), recipe.ModifiedDate);
        recipe.ModifiedDate = recipe.ModifiedDate.AddMinutes(5);

        draft.IsStaleFor(recipe).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAndRestore_ThroughDraftService_KeepsExactModifiedDateAndNullQuantities()
    {
        // The stale check compares dates exactly, so the JSON round trip must not lose precision
        var store = new InMemoryDraftStore();
        var drafts = new DraftService(store, new ManualTimeProvider(), NullLogger<DraftService>.Instance);
        var recipe = SampleRecipe(id: 7);
        await drafts.SaveAsync("key", RecipeDraft.Capture(recipe, SampleEntries(), recipe.ModifiedDate), DraftLifetime.Device);

        var restored = (await drafts.RestoreAsync<RecipeDraft>("key", DraftLifetime.Device))!.Value;

        restored.BaseModifiedDate.ShouldBe(recipe.ModifiedDate);
        restored.IsStaleFor(recipe).ShouldBeFalse();
        restored.Ingredients[1].Quantity.ShouldBeNull();
    }
}
