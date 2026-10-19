using Bunit;
using RecipesApp.Pages;
using RecipesApp.Services.Drafts;
using Shouldly;

namespace Recipes.Tests.Components;

public sealed class GroceryListDraftTests : ComponentTestBase
{
    [Fact]
    public void SelectingARecipe_SavesDraft()
    {
        var id = Db.SeedRecipe("Pancakes", "Flour", "Eggs");
        var cut = Render<GroceryList>();

        cut.Find($"#recipe-{id}").Change(true);

        cut.WaitForAssertion(() => HasDraft(GroceryListDraft.Key).ShouldBeTrue());
    }

    [Fact]
    public void GeneratingAList_IsKeptInTheDraft()
    {
        var id = Db.SeedRecipe("Pancakes", "Flour", "Eggs");
        var cut = Render<GroceryList>();
        cut.Find($"#recipe-{id}").Change(true);

        cut.FindAll("button").Single(b => b.TextContent.Contains("Generate List")).Click();

        // Synchronous check of the stored draft: WaitForAssertion takes an Action, and an async
        // lambda there would become async void and could never fail the test
        cut.WaitForAssertion(() =>
            Store.Values[(DraftLifetime.Device, GroceryListDraft.Key)].ShouldContain("Flour"));
    }

    [Fact]
    public async Task OpeningThePage_WithDraft_RestoresSelectionAndGeneratedList()
    {
        var id = Db.SeedRecipe("Pancakes", "Flour", "Eggs");
        await Drafts.SaveAsync(GroceryListDraft.Key, new GroceryListDraft(
            Multipliers: new SortedDictionary<int, int> { [id] = 3 },
            RecipeNames: new SortedDictionary<int, string> { [id] = "Pancakes" },
            GeneratedList: "- 3 piece(s) Eggs\n- 3 piece(s) Flour",
            SearchTerm: "",
            ShowFilters: false,
            MinCookTime: null, MaxCookTime: null, MinServings: null, MaxServings: null,
            Tags: new List<string>()), DraftLifetime.Device);

        var cut = Render<GroceryList>();

        cut.WaitForAssertion(() => cut.Find($"#recipe-{id}").HasAttribute("checked").ShouldBeTrue());
        cut.Markup.ShouldContain("3 x Pancakes");
        cut.Find("textarea").TextContent.ShouldContain("3 piece(s) Flour");
    }

    [Fact]
    public void ClearSelection_DeletesDraft()
    {
        var id = Db.SeedRecipe("Pancakes", "Flour");
        var cut = Render<GroceryList>();
        cut.Find($"#recipe-{id}").Change(true);
        cut.WaitForAssertion(() => HasDraft(GroceryListDraft.Key).ShouldBeTrue());

        cut.FindAll("button").Single(b => b.TextContent.Contains("Clear Selection")).Click();

        cut.WaitForAssertion(() => HasDraft(GroceryListDraft.Key).ShouldBeFalse());
    }
}
