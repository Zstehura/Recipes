using Bunit;
using Shouldly;

namespace Recipes.Tests.Components;

public sealed class RecipeListTests : ComponentTestBase
{
    private IRenderedComponent<RecipesApp.Pages.Index> RenderList() => Render<RecipesApp.Pages.Index>();

    private static string[] Titles(IRenderedComponent<RecipesApp.Pages.Index> cut) =>
        cut.FindAll(".recipe-item-title").Select(h => h.TextContent.Trim()).Order().ToArray();

    [Fact]
    public void EachRecipe_IsOneLinkToItsPage_WithNoEditOrDeleteButtons()
    {
        var id = Db.SeedRecipe("Pancakes");

        var cut = RenderList();

        cut.WaitForAssertion(() => cut.FindAll("a.recipe-item").Count.ShouldBe(1));
        cut.Find("a.recipe-item").GetAttribute("href").ShouldBe($"recipe/{id}");
        cut.FindAll("button").Select(b => b.TextContent).ShouldNotContain(t => t.Contains("Delete"));
        cut.FindAll("a").Select(a => a.TextContent.Trim()).ShouldNotContain("Edit");
    }

    [Fact]
    public void NoRecipes_InvitesAddingTheFirstOne()
    {
        var cut = RenderList();

        cut.WaitForAssertion(() => cut.Find(".empty-state").TextContent.ShouldContain("No recipes yet"));
        cut.Find(".empty-state a").GetAttribute("href").ShouldBe("recipe/new");
    }

    [Fact]
    public void SearchWithNoMatches_SaysSo_AndClearingShowsEverythingAgain()
    {
        Db.SeedRecipe("Pancakes");
        Db.SeedRecipe("Lasagna");
        var cut = RenderList();

        cut.Find("#recipe-search").Input("zzz");

        cut.WaitForAssertion(() => cut.Find(".empty-state").TextContent.ShouldContain("No recipes match"));
        cut.Find(".empty-state button").Click();

        cut.WaitForAssertion(() => Titles(cut).ShouldBe(new[] { "Lasagna", "Pancakes" }));
        cut.Find("#recipe-search").GetAttribute("value").ShouldBeNullOrEmpty();
    }

    [Fact]
    public void Searching_ShowsHowManyMatch()
    {
        Db.SeedRecipe("Pancakes");
        Db.SeedRecipe("Lasagna");
        var cut = RenderList();

        cut.Find("#recipe-search").Input("lasag");

        cut.WaitForAssertion(() => Titles(cut).ShouldBe(new[] { "Lasagna" }));
        cut.Find("[role=status]").TextContent.Trim().ShouldBe("1 recipe matches");
    }

    [Fact]
    public void Filters_AreHiddenUntilTheButtonIsPressed()
    {
        Db.SeedRecipe("Pancakes", tags: "Breakfast");
        var cut = RenderList();
        var toggle = cut.Find("button[aria-controls=recipe-filters]");
        toggle.GetAttribute("aria-expanded").ShouldBe("false");
        cut.FindAll("#recipe-filters").ShouldBeEmpty();

        toggle.Click();

        cut.Find("button[aria-controls=recipe-filters]").GetAttribute("aria-expanded").ShouldBe("true");
        cut.FindAll("#recipe-filters").Count.ShouldBe(1);
    }

    [Fact]
    public void TagChip_FiltersTheList_AndTheButtonShowsHowManyFiltersAreOn()
    {
        Db.SeedRecipe("Pancakes", tags: "Breakfast");
        Db.SeedRecipe("Lasagna", tags: "Dinner, Italian");
        var cut = RenderList();
        cut.Find("button[aria-controls=recipe-filters]").Click();

        cut.Find(".tag-chip:contains('Dinner')").Click();

        cut.WaitForAssertion(() => Titles(cut).ShouldBe(new[] { "Lasagna" }));
        cut.Find(".tag-chip:contains('Dinner')").GetAttribute("aria-pressed").ShouldBe("true");
        cut.Find("button[aria-controls=recipe-filters] .badge").TextContent.ShouldStartWith("1");
    }

    [Fact]
    public void CookingTimeFilter_KeepsOnlyRecipesInRange()
    {
        Db.SeedRecipe("Salad", cookingTime: 10);
        Db.SeedRecipe("Stew", cookingTime: 120);
        var cut = RenderList();
        cut.Find("button[aria-controls=recipe-filters]").Click();

        cut.Find("#recipe-filters-cook-max").Change(30);

        cut.WaitForAssertion(() => Titles(cut).ShouldBe(new[] { "Salad" }));
    }

    [Fact]
    public void EveryFilterInput_HasALabel()
    {
        Db.SeedRecipe("Pancakes");
        var cut = RenderList();
        cut.Find("button[aria-controls=recipe-filters]").Click();

        foreach (var input in cut.FindAll("input"))
        {
            var id = input.GetAttribute("id");
            id.ShouldNotBeNullOrEmpty();
            cut.FindAll($"label[for='{id}']").Count.ShouldBe(1, $"input #{id} has no label");
        }
    }
}
