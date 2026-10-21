using Bunit;
using RecipesApp.Pages;
using Shouldly;

namespace Recipes.Tests.Components;

public sealed class SearchByIngredientsTests : ComponentTestBase
{
    public SearchByIngredientsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;   // focusing the results heading
    }

    private int IngredientId(string name)
    {
        using var context = Db.CreateContext();
        return context.Ingredients.Single(i => i.Name == name).Id;
    }

    private static string[] Results(IRenderedComponent<SearchByIngredients> cut) =>
        cut.FindAll(".recipe-item-title").Select(t => t.TextContent.Trim()).Order().ToArray();

    private static string[] PickerNames(IRenderedComponent<SearchByIngredients> cut) =>
        cut.FindAll(".ingredient-picker label").Select(l => l.TextContent.Trim()).ToArray();

    [Fact]
    public void BeforePicking_AsksForAnIngredient_AndHasNoResultsBar()
    {
        Db.SeedRecipe("Pancakes", "Flour", "Eggs");

        var cut = Render<SearchByIngredients>();

        cut.Find("[role=status]").TextContent.ShouldContain("Pick at least one ingredient");
        cut.FindAll(".action-bar").ShouldBeEmpty();
        cut.FindAll("button").ShouldNotContain(b => b.TextContent.Contains("Select All"));
    }

    [Fact]
    public void PickingAnIngredient_ShowsRecipesThatUseIt_AsLinks()
    {
        var pancakes = Db.SeedRecipe("Pancakes", "Flour", "Eggs");
        Db.SeedRecipe("Salad", "Lettuce");
        var cut = Render<SearchByIngredients>();

        cut.Find($"#ingredient-{IngredientId("Flour")}").Change(true);

        cut.WaitForAssertion(() => Results(cut).ShouldBe(new[] { "Pancakes" }));
        cut.Find("a.recipe-item").GetAttribute("href").ShouldBe($"recipe/{pancakes}");
        cut.Find(".action-bar-status").TextContent.Trim().ShouldBe("1 recipe matches");
    }

    [Fact]
    public void PickingSeveral_KeepsOnlyRecipesThatUseAllOfThem()
    {
        Db.SeedRecipe("Pancakes", "Flour", "Eggs");
        Db.SeedRecipe("Omelette", "Eggs");
        var cut = Render<SearchByIngredients>();

        cut.Find($"#ingredient-{IngredientId("Eggs")}").Change(true);
        cut.WaitForAssertion(() => Results(cut).ShouldBe(new[] { "Omelette", "Pancakes" }));
        cut.Find($"#ingredient-{IngredientId("Flour")}").Change(true);

        cut.WaitForAssertion(() => Results(cut).ShouldBe(new[] { "Pancakes" }));
    }

    [Fact]
    public void SelectedIngredients_AppearAsChipsThatRemoveThem()
    {
        Db.SeedRecipe("Pancakes", "Flour", "Eggs");
        var cut = Render<SearchByIngredients>();
        var flour = IngredientId("Flour");
        cut.Find($"#ingredient-{flour}").Change(true);

        cut.Find("button[aria-label='Remove Flour']").Click();

        cut.FindAll("button[aria-label='Remove Flour']").ShouldBeEmpty();
        cut.Find($"#ingredient-{flour}").HasAttribute("checked").ShouldBeFalse();
        cut.Find("[role=status]").TextContent.ShouldContain("Pick at least one ingredient");
    }

    [Fact]
    public void TypingInTheFinder_NarrowsTheList_ButKeepsSelectionsAsChips()
    {
        Db.SeedRecipe("Pancakes", "Flour", "Eggs", "Milk");
        var cut = Render<SearchByIngredients>();
        cut.Find($"#ingredient-{IngredientId("Eggs")}").Change(true);

        cut.Find("#ingredient-filter").Input("fl");

        PickerNames(cut).ShouldBe(new[] { "Flour" });
        cut.FindAll("button[aria-label='Remove Eggs']").Count.ShouldBe(1);
    }

    [Fact]
    public void FinderWithNoMatch_SaysSo()
    {
        Db.SeedRecipe("Pancakes", "Flour");
        var cut = Render<SearchByIngredients>();

        cut.Find("#ingredient-filter").Input("zzz");

        PickerNames(cut).ShouldBeEmpty();
        cut.Markup.ShouldContain("No ingredient matches");
    }

    [Fact]
    public void NoRecipeUsesThemAll_SuggestsRemovingOne()
    {
        Db.SeedRecipe("Pancakes", "Flour");
        Db.SeedRecipe("Salad", "Lettuce");
        var cut = Render<SearchByIngredients>();

        cut.Find($"#ingredient-{IngredientId("Flour")}").Change(true);
        cut.Find($"#ingredient-{IngredientId("Lettuce")}").Change(true);

        cut.WaitForAssertion(() => cut.Find(".empty-state").TextContent.ShouldContain("Try removing an ingredient"));
        cut.Find(".action-bar button").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void ShowRecipes_MovesFocusToTheResults()
    {
        Db.SeedRecipe("Pancakes", "Flour");
        var cut = Render<SearchByIngredients>();
        cut.Find($"#ingredient-{IngredientId("Flour")}").Change(true);

        cut.Find(".action-bar button").Click();

        cut.WaitForAssertion(() => JSInterop.VerifyFocusAsyncInvoke());
    }

    [Fact]
    public void Filters_UseTheSharedPanel()
    {
        Db.SeedRecipe("Quick eggs", new (string, decimal?, string?, string?)[] { ("Eggs", 2m, "pieces", null) }, cookingTime: 5);
        Db.SeedRecipe("Slow eggs", new (string, decimal?, string?, string?)[] { ("Eggs", 2m, "pieces", null) }, cookingTime: 90);
        var cut = Render<SearchByIngredients>();
        cut.Find($"#ingredient-{IngredientId("Eggs")}").Change(true);
        cut.Find("button[aria-controls=ingredient-filters]").Click();

        cut.Find("#ingredient-filters-cook-max").Change(30);

        cut.WaitForAssertion(() => Results(cut).ShouldBe(new[] { "Quick eggs" }));
    }

    [Fact]
    public void EveryInput_HasALabel()
    {
        Db.SeedRecipe("Pancakes", "Flour");
        var cut = Render<SearchByIngredients>();
        cut.Find($"#ingredient-{IngredientId("Flour")}").Change(true);
        cut.Find("button[aria-controls=ingredient-filters]").Click();

        foreach (var input in cut.FindAll("input"))
        {
            var id = input.GetAttribute("id");
            id.ShouldNotBeNullOrEmpty();
            cut.FindAll($"label[for='{id}']").Count.ShouldBe(1, $"#{id} has no label");
        }
    }
}
