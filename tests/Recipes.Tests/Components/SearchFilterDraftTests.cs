using Bunit;
using RecipesApp.Pages;
using RecipesApp.Services;
using RecipesApp.Services.Drafts;
using Shouldly;

namespace Recipes.Tests.Components;

public sealed class SearchFilterDraftTests : ComponentTestBase
{
    // ---------------------------------------------------------------- Recipe list (Index)

    [Fact]
    public void RecipeList_TypingASearch_SavesFiltersForThisTabOnly()
    {
        Db.SeedRecipe("Pancakes");
        var cut = Render<RecipesApp.Pages.Index>();

        cut.Find("#recipe-search").Input("pan");

        cut.WaitForAssertion(() => HasDraft(RecipeFilters.RecipeListKey, DraftLifetime.Tab).ShouldBeTrue());
        HasDraft(RecipeFilters.RecipeListKey, DraftLifetime.Device).ShouldBeFalse();
    }

    [Fact]
    public async Task RecipeList_WithSavedFilters_RestoresSearchAndResults()
    {
        Db.SeedRecipe("Pancakes");
        Db.SeedRecipe("Lasagna");
        await Drafts.SaveAsync(RecipeFilters.RecipeListKey,
            new RecipeFilters("lasag", false, null, null, null, null, new List<string>()), DraftLifetime.Tab);

        var cut = Render<RecipesApp.Pages.Index>();

        cut.WaitForAssertion(() => cut.Find("#recipe-search").GetAttribute("value").ShouldBe("lasag"));
        cut.FindAll(".recipe-item-title").Select(h => h.TextContent.Trim()).ShouldBe(new[] { "Lasagna" });
    }

    // ---------------------------------------------------------------- Search by ingredients

    [Fact]
    public void IngredientSearch_SelectingAnIngredient_SavesSelection()
    {
        Db.SeedRecipe("Pancakes", "Flour");
        var cut = Render<SearchByIngredients>();
        var flour = cut.Find("label:contains('Flour')").GetAttribute("for")!;

        cut.Find($"#{flour}").Change(true);

        cut.WaitForAssertion(() => HasDraft(IngredientSearchDraft.Key, DraftLifetime.Tab).ShouldBeTrue());
    }

    [Fact]
    public async Task IngredientSearch_WithSavedSelection_RestoresAndSearches_IgnoringDeletedIngredients()
    {
        Db.SeedRecipe("Pancakes", "Flour", "Eggs");
        Db.SeedRecipe("Omelette", "Eggs");
        int eggsId;
        using (var context = Db.CreateContext())
        {
            eggsId = context.Ingredients.Single(i => i.Name == "Eggs").Id;
        }
        const int deletedIngredientId = 9999;
        await Drafts.SaveAsync(IngredientSearchDraft.Key, new IngredientSearchDraft(
            new List<int> { eggsId, deletedIngredientId },
            new RecipeFilters("", false, null, null, null, null, new List<string>())), DraftLifetime.Tab);

        var cut = Render<SearchByIngredients>();

        cut.WaitForAssertion(() => cut.Find($"#ingredient-{eggsId}").HasAttribute("checked").ShouldBeTrue());
        cut.FindAll(".recipe-item-title").Select(h => h.TextContent.Trim()).Order()
            .ShouldBe(new[] { "Omelette", "Pancakes" });
    }

    [Fact]
    public async Task IngredientSearch_SavedMatch_IsRestored()
    {
        Db.SeedRecipe("Omelette", "Eggs", "Salt");
        Db.SeedRecipe("Frittata", "Eggs", "Cheese");
        int eggsId;
        using (var context = Db.CreateContext())
        {
            eggsId = context.Ingredients.Single(i => i.Name == "Eggs").Id;
        }
        await Drafts.SaveAsync(IngredientSearchDraft.Key, new IngredientSearchDraft(
            new List<int> { eggsId },
            new RecipeFilters("", false, null, null, null, null, new List<string>()),
            IngredientMatch.OnlyThese), DraftLifetime.Tab);

        var cut = Render<SearchByIngredients>();

        cut.WaitForAssertion(() => cut.Find("#match-only-these").HasAttribute("checked").ShouldBeTrue());
        cut.FindAll(".recipe-item-title").Select(h => h.TextContent.Trim()).ShouldBe(new[] { "Omelette" });
    }

    [Fact]
    public void IngredientSearch_DraftFromBeforeTheMatchChoice_UsesAllOfThem()
    {
        Db.SeedRecipe("Omelette", "Eggs", "Salt");
        Db.SeedRecipe("Frittata", "Eggs", "Cheese");
        int eggsId;
        using (var context = Db.CreateContext())
        {
            eggsId = context.Ingredients.Single(i => i.Name == "Eggs").Id;
        }
        // Saved by the previous version, which had no "match" field
        Store.Values[(DraftLifetime.Tab, IngredientSearchDraft.Key)] =
            "{\"version\":1,\"savedAt\":\"2026-10-01T12:00:00+00:00\",\"payload\":{\"selectedIngredientIds\":[" + eggsId + "]," +
            "\"filters\":{\"searchTerm\":\"\",\"showFilters\":false,\"tags\":[]}}}";

        var cut = Render<SearchByIngredients>();

        cut.WaitForAssertion(() => cut.FindAll(".recipe-item-title").Count.ShouldBe(2));
        cut.Find("#match-uses-all").HasAttribute("checked").ShouldBeTrue();
    }
}
