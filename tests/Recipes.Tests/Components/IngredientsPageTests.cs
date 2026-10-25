using Bunit;
using RecipesApp.Models;
using RecipesApp.Pages;
using Shouldly;

namespace Recipes.Tests.Components;

public sealed class IngredientsPageTests : ComponentTestBase
{
    public IngredientsPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;   // dialogs and focusing the edit field
    }

    private int IngredientId(string name)
    {
        using var context = Db.CreateContext();
        return context.Ingredients.Single(i => i.Name == name).Id;
    }

    private static string[] Names(IRenderedComponent<Ingredients> cut) =>
        cut.FindAll(".manage-row-name").Select(n => n.TextContent.Trim()).ToArray();

    // Opens the "⋯" menu for one ingredient and chooses an item from it
    private static void Choose(IRenderedComponent<Ingredients> cut, string ingredient, string action)
    {
        cut.Find($"button[aria-label='Actions for {ingredient}']").Click();
        cut.FindAll(".overflow-menu-item").Single(b => b.TextContent.Trim() == action).Click();
    }

    private static string Message(IRenderedComponent<Ingredients> cut) =>
        cut.Find(".page-message").TextContent.Trim();

    [Fact]
    public void Rows_ShowTheUnitAndHowManyRecipesUseIt()
    {
        Db.SeedRecipe("Pancakes", "Flour", "Eggs");
        Db.SeedRecipe("Omelette", "Eggs");
        Db.SeedIngredient("Saffron", MeasurementUnit.Grams);

        var cut = Render<Ingredients>();

        Names(cut).ShouldBe(new[] { "Eggs", "Flour", "Saffron" });
        var meta = cut.FindAll(".manage-row-meta").Select(m => m.TextContent.Trim()).ToArray();
        meta[0].ShouldEndWith("Used in 2 recipes");
        meta[1].ShouldEndWith("Used in 1 recipe");
        meta[2].ShouldStartWith("Default unit: g ");
        meta[2].ShouldEndWith("Not used");
    }

    [Fact]
    public void Search_NarrowsTheList()
    {
        Db.SeedRecipe("Pancakes", "Flour", "Eggs", "Milk");
        var cut = Render<Ingredients>();

        cut.Find("#ingredient-search").Input("fl");

        Names(cut).ShouldBe(new[] { "Flour" });
        cut.Find("[role=status]").TextContent.Trim().ShouldBe("1 of 3");
    }

    [Fact]
    public void Rename_OpensAFocusedField_AndSavesTheName()
    {
        Db.SeedRecipe("Pancakes", "Flour");
        var cut = Render<Ingredients>();
        var id = IngredientId("Flour");

        Choose(cut, "Flour", "Rename");
        cut.WaitForAssertion(() => JSInterop.VerifyFocusAsyncInvoke());
        cut.Find($"#rename-{id}").Input("Plain flour");
        cut.Find("form.manage-row-edit").Submit();

        Names(cut).ShouldBe(new[] { "Plain flour" });
        Message(cut).ShouldContain("renamed");
        cut.FindAll("form.manage-row-edit").ShouldBeEmpty();
    }

    [Fact]
    public void Rename_ToAnExistingName_ShowsAnError_AndKeepsTheField()
    {
        Db.SeedRecipe("Pancakes", "Flour", "Eggs");
        var cut = Render<Ingredients>();

        Choose(cut, "Flour", "Rename");
        cut.Find($"#rename-{IngredientId("Flour")}").Input("Eggs");
        cut.Find("form.manage-row-edit").Submit();

        cut.Find(".page-message .alert-danger").TextContent.ShouldContain("Couldn't rename");
        cut.FindAll("form.manage-row-edit").Count.ShouldBe(1);
    }

    [Fact]
    public void Escape_ClosesTheEditField()
    {
        Db.SeedRecipe("Pancakes", "Flour");
        var cut = Render<Ingredients>();
        Choose(cut, "Flour", "Rename");

        cut.Find("form.manage-row-edit").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        cut.FindAll("form.manage-row-edit").ShouldBeEmpty();
    }

    [Fact]
    public void ChangeDefaultUnit_SavesIt()
    {
        Db.SeedRecipe("Pancakes", "Flour");
        var cut = Render<Ingredients>();
        var id = IngredientId("Flour");

        Choose(cut, "Flour", "Change default unit");
        cut.Find($"#unit-{id}").Change(MeasurementUnit.Grams.ToString());
        cut.Find("form.manage-row-edit").Submit();

        cut.Find(".manage-row-meta").TextContent.ShouldContain("Default unit: g ");
        Message(cut).ShouldContain("Default unit updated");
    }

    [Fact]
    public void Merge_NeedsATarget_ThenMovesTheRecipesOver()
    {
        Db.SeedRecipe("Pancakes", "Flour");
        Db.SeedRecipe("Bread", "Plain flour");
        var cut = Render<Ingredients>();

        Choose(cut, "Plain flour", "Merge into another");
        var merge = cut.FindAll("dialog .btn-danger").Single(b => b.TextContent.Trim() == "Merge");
        merge.HasAttribute("disabled").ShouldBeTrue();
        cut.FindAll("#merge-target option").Select(o => o.TextContent).ShouldNotContain("Plain flour");

        cut.Find("#merge-target").Change(IngredientId("Flour").ToString());
        cut.FindAll("dialog .btn-danger").Single(b => b.TextContent.Trim() == "Merge").Click();

        Names(cut).ShouldBe(new[] { "Flour" });
        cut.Find(".manage-row-meta").TextContent.ShouldEndWith("Used in 2 recipes");
        Message(cut).ShouldContain("Merged 'Plain flour' into 'Flour'");
    }

    [Fact]
    public void Delete_ListsTheRecipesFirst_AndRemovesItOnConfirm()
    {
        Db.SeedRecipe("Pancakes", "Flour", "Eggs");
        var cut = Render<Ingredients>();

        Choose(cut, "Flour", "Delete");
        cut.FindAll("dialog")[1].TextContent.ShouldContain("Pancakes");
        cut.FindAll("dialog .btn-danger").Single(b => b.TextContent.Trim() == "Delete").Click();

        Names(cut).ShouldBe(new[] { "Eggs" });
        Message(cut).ShouldContain("'Flour' deleted");
    }

    [Fact]
    public void CancellingDelete_KeepsTheIngredient()
    {
        Db.SeedRecipe("Pancakes", "Flour");
        var cut = Render<Ingredients>();

        Choose(cut, "Flour", "Delete");
        cut.FindAll("dialog")[1].QuerySelector("button[autofocus]")!.Click();

        Names(cut).ShouldBe(new[] { "Flour" });
        Message(cut).ShouldBeEmpty();
    }

    [Fact]
    public void Messages_StayUntilDismissed()
    {
        Db.SeedRecipe("Pancakes", "Flour");
        var cut = Render<Ingredients>();
        Choose(cut, "Flour", "Change default unit");
        cut.Find("form.manage-row-edit").Submit();

        cut.Find(".page-message").GetAttribute("aria-live").ShouldBe("polite");
        Message(cut).ShouldNotBeEmpty();
        cut.Find("button[aria-label='Dismiss message']").Click();

        Message(cut).ShouldBeEmpty();
    }

    [Fact]
    public void NoIngredients_PointsToAddingARecipe()
    {
        var cut = Render<Ingredients>();

        cut.Find(".empty-state a").GetAttribute("href").ShouldBe("recipe/new");
    }

    [Fact]
    public void EveryField_HasALabel()
    {
        Db.SeedRecipe("Pancakes", "Flour", "Eggs");
        var cut = Render<Ingredients>();
        Choose(cut, "Flour", "Rename");

        foreach (var field in cut.FindAll("input, select"))
        {
            var id = field.GetAttribute("id");
            id.ShouldNotBeNullOrEmpty($"a {field.TagName} has no id");
            cut.FindAll($"label[for='{id}']").Count.ShouldBe(1, $"#{id} has no label");
        }
    }
}
