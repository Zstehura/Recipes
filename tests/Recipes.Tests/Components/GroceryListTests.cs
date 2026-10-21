using Bunit;
using Microsoft.JSInterop;
using RecipesApp.Pages;
using RecipesApp.Services.Drafts;
using Shouldly;

namespace Recipes.Tests.Components;

public sealed class GroceryListTests : ComponentTestBase
{
    public GroceryListTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;   // unplanned calls return defaults, so canShare is false
    }

    private int SeedPancakes() => Db.SeedRecipe("Pancakes",
        new (string, decimal?, string?, string?)[] { ("Flour", 200m, "g", null) }, tags: "Breakfast");

    // A page button by its text (the confirmation dialog's buttons are always in the page too)
    private static void Click(IRenderedComponent<GroceryList> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim().StartsWith(text) && b.Closest("dialog") == null).Click();

    private static IRenderedComponent<GroceryList> MakeListFor(IRenderedComponent<GroceryList> cut, int recipeId)
    {
        cut.Find($"#recipe-{recipeId}").Change(true);
        Click(cut, "Make list");
        cut.WaitForAssertion(() => cut.FindAll(".grocery-text").Count.ShouldBe(1));
        return cut;
    }

    [Fact]
    public void MakeList_IsDisabledUntilARecipeIsPicked()
    {
        var id = SeedPancakes();
        var cut = Render<GroceryList>();
        var makeList = () => cut.FindAll("button").Single(b => b.TextContent.Contains("Make list"));

        makeList().HasAttribute("disabled").ShouldBeTrue();
        cut.Find($"#recipe-{id}").Change(true);

        makeList().HasAttribute("disabled").ShouldBeFalse();
        cut.Find(".action-bar-status").TextContent.Trim().ShouldBe("1 recipe selected");
    }

    [Fact]
    public void Stepper_AppearsOnPickedRecipes_AndScalesTheList()
    {
        var id = SeedPancakes();
        var cut = Render<GroceryList>();
        cut.FindAll(".stepper").ShouldBeEmpty();
        cut.Find($"#recipe-{id}").Change(true);

        cut.Find(".stepper button[aria-label=Increase]").Click();
        cut.Find(".stepper-value").TextContent.ShouldBe("2×");
        Click(cut, "Make list");

        cut.WaitForAssertion(() => cut.Find(".grocery-text").TextContent.ShouldContain("400 g Flour"));
        cut.Find(".grocery-result").TextContent.ShouldContain("From 2 × Pancakes");
    }

    [Fact]
    public void MakingAList_ShowsOnlyTheList_AndChangeRecipesGoesBack()
    {
        var id = SeedPancakes();
        var cut = MakeListFor(Render<GroceryList>(), id);

        cut.FindAll(".pick-list").ShouldBeEmpty();
        Click(cut, "Change recipes");

        cut.Find($"#recipe-{id}").HasAttribute("checked").ShouldBeTrue();
        Click(cut, "View list");
        cut.FindAll(".grocery-text").Count.ShouldBe(1);
    }

    [Fact]
    public void StartOver_WithAList_AsksFirst()
    {
        var id = SeedPancakes();
        var cut = MakeListFor(Render<GroceryList>(), id);
        cut.WaitForAssertion(() => HasDraft(GroceryListDraft.Key).ShouldBeTrue());

        Click(cut, "Start over");

        JSInterop.VerifyInvoke("appDialog.show");
        HasDraft(GroceryListDraft.Key).ShouldBeTrue();
        cut.FindAll(".grocery-text").Count.ShouldBe(1);
    }

    [Fact]
    public void StartOver_Confirmed_ClearsEverything()
    {
        var id = SeedPancakes();
        var cut = MakeListFor(Render<GroceryList>(), id);
        Click(cut, "Start over");

        cut.Find("dialog button.btn-danger").Click();

        cut.WaitForAssertion(() => HasDraft(GroceryListDraft.Key).ShouldBeFalse());
        cut.Find($"#recipe-{id}").HasAttribute("checked").ShouldBeFalse();
    }

    [Fact]
    public void Copy_UsesTheCopyHelper_AndSaysSo()
    {
        var id = SeedPancakes();
        var cut = MakeListFor(Render<GroceryList>(), id);

        Click(cut, "Copy");

        JSInterop.VerifyInvoke("appShare.copy").Arguments[0]!.ToString()!.ShouldContain("200 g Flour");
        cut.WaitForAssertion(() => cut.Find(".grocery-result [role=status]").TextContent.ShouldBe("Copied."));
    }

    [Fact]
    public void Copy_Refused_SuggestsSelectingByHand()
    {
        JSInterop.SetupVoid("appShare.copy", _ => true).SetException(new JSException("not allowed"));
        var id = SeedPancakes();
        var cut = MakeListFor(Render<GroceryList>(), id);

        Click(cut, "Copy");

        cut.WaitForAssertion(() =>
            cut.Find(".grocery-result [role=status]").TextContent.ShouldContain("Press and hold the list"));
    }

    [Fact]
    public void Share_IsOnlyOfferedWhereTheBrowserSupportsIt()
    {
        var id = SeedPancakes();
        var withoutShare = MakeListFor(Render<GroceryList>(), id);
        withoutShare.FindAll("button").ShouldNotContain(b => b.TextContent.Contains("Share"));

        JSInterop.Setup<bool>("appShare.canShare").SetResult(true);
        var withShare = Render<GroceryList>();   // restores the list from the draft

        withShare.WaitForAssertion(() =>
            withShare.FindAll("button").ShouldContain(b => b.TextContent.Contains("Share")));
    }

    [Fact]
    public void Filters_UseTheSharedPanel()
    {
        SeedPancakes();
        Db.SeedRecipe("Lasagna", tags: "Dinner");
        var cut = Render<GroceryList>();
        cut.Find("button[aria-controls=grocery-filters]").Click();

        cut.Find(".tag-chip:contains('Dinner')").Click();

        cut.WaitForAssertion(() =>
            cut.FindAll(".pick-row-name").Select(n => n.TextContent).ShouldBe(new[] { "Lasagna" }));
    }
}
