using Bunit;
using RecipesApp.Pages;
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
        cut.FindAll("h5.card-title").Select(h => h.TextContent.Trim())
            .Where(t => t != "Filter Recipes").Order().ShouldBe(new[] { "Omelette", "Pancakes" });
    }
}
