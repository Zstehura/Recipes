using Bunit;
using RecipesApp.Pages;
using Shouldly;

namespace Recipes.Tests.Components;

public sealed class RecipeFormLayoutTests : ComponentTestBase
{
    public RecipeFormLayoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;   // focusing the new ingredient row
    }

    private IRenderedComponent<RecipeForm> RenderNew() => Render<RecipeForm>();

    private IRenderedComponent<RecipeForm> RenderEdit(int id) => Render<RecipeForm>(p => p.Add(f => f.Id, id));

    [Fact]
    public void EveryField_HasALabel()
    {
        var cut = RenderNew();

        foreach (var field in cut.FindAll("input, select, textarea"))
        {
            var id = field.GetAttribute("id");
            id.ShouldNotBeNullOrEmpty($"a {field.TagName} has no id");
            cut.FindAll($"label[for='{id}']").Count.ShouldBe(1, $"#{id} has no label");
        }
    }

    [Fact]
    public void Amounts_AskForTheNumberKeypad()
    {
        var cut = RenderNew();

        cut.Find("#ing-0-qty").GetAttribute("inputmode").ShouldBe("decimal");
        cut.Find("#cookingTime").GetAttribute("inputmode").ShouldBe("numeric");
        cut.Find("#servings").GetAttribute("inputmode").ShouldBe("numeric");
    }

    [Fact]
    public void AddIngredient_AddsARow_AndFocusesIt()
    {
        var cut = RenderNew();
        cut.FindAll(".ingredient-row").Count.ShouldBe(1);

        cut.FindAll("button").Single(b => b.TextContent.Contains("Add ingredient")).Click();

        cut.FindAll(".ingredient-row").Count.ShouldBe(2);
        cut.WaitForAssertion(() => JSInterop.VerifyFocusAsyncInvoke());
    }

    [Fact]
    public void RemoveButton_SaysWhichIngredient()
    {
        var cut = RenderNew();
        cut.Find("#ing-0-name").Change("Flour");

        cut.Find(".ingredient-remove").GetAttribute("aria-label").ShouldBe("Remove ingredient 1: Flour");
    }

    [Fact]
    public void RemovingTheLastIngredient_AsksForOne()
    {
        var cut = RenderNew();

        cut.Find(".ingredient-remove").Click();

        cut.FindAll(".ingredient-row").ShouldBeEmpty();
        cut.Markup.ShouldContain("Add at least one ingredient.");
    }

    [Fact]
    public void MoreDetails_IsClosedForANewRecipe()
    {
        var cut = RenderNew();

        cut.Find("details.form-extras").HasAttribute("open").ShouldBeFalse();
    }

    [Fact]
    public void MoreDetails_IsOpenWhenTheRecipeHasTags()
    {
        var id = Db.SeedRecipe("Lasagna", tags: "Dinner");

        var cut = RenderEdit(id);

        cut.WaitForAssertion(() => cut.Find("details.form-extras").HasAttribute("open").ShouldBeTrue());
    }

    [Fact]
    public void SaveAndCancel_AreInTheActionBar()
    {
        var cut = RenderNew();

        var buttons = cut.FindAll(".action-bar button");
        buttons.Select(b => b.TextContent.Trim()).ShouldBe(new[] { "Cancel", "Save recipe" });
        buttons[1].GetAttribute("type").ShouldBe("submit");
    }

    [Fact]
    public void SubmittingAnEmptyForm_ShowsErrorsAboveTheSaveButton()
    {
        var cut = RenderNew();

        cut.Find("form").Submit();

        var alert = cut.Find("form [role=alert]");
        alert.TextContent.ShouldContain("Recipe name is required");
        alert.TextContent.ShouldContain("Instructions are required");
    }
}
