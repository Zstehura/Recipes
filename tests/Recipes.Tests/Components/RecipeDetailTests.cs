using Bunit;
using RecipesApp.Pages;
using RecipesApp.Services.Drafts;
using Shouldly;

namespace Recipes.Tests.Components;

public sealed class RecipeDetailTests : ComponentTestBase
{
    public RecipeDetailTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;   // the confirmation dialog and menu focus use JS interop
    }

    private int SeedPancakes(DateTime? modified = null) => Db.SeedRecipe("Pancakes",
        new (string, decimal?, string?, string?)[] { ("Flour", 200m, "g", null), ("Eggs", 2m, "pieces", null) },
        modified: modified);

    private IRenderedComponent<RecipeDetail> RenderDetail(int id) =>
        Render<RecipeDetail>(p => p.Add(d => d.Id, id));

    private bool RecipeExists(int id)
    {
        using var context = Db.CreateContext();
        return context.Recipes.Any(r => r.Id == id);
    }

    private List<int> LineIds(int recipeId)
    {
        using var context = Db.CreateContext();
        return context.RecipeIngredients.Where(l => l.RecipeId == recipeId).OrderBy(l => l.Id).Select(l => l.Id).ToList();
    }

    private static void ChooseDelete(IRenderedComponent<RecipeDetail> cut)
    {
        cut.Find("button[aria-label='Recipe actions']").Click();
        cut.Find("button.overflow-menu-item").Click();
    }

    [Fact]
    public void Delete_AsksFirst_AndDoesNothingUntilConfirmed()
    {
        var id = SeedPancakes();
        var cut = RenderDetail(id);

        ChooseDelete(cut);

        cut.WaitForAssertion(() => JSInterop.VerifyInvoke("appDialog.show"));
        RecipeExists(id).ShouldBeTrue();
    }

    [Fact]
    public void Delete_Confirmed_DeletesAndGoesBackToTheList()
    {
        var id = SeedPancakes();
        var cut = RenderDetail(id);
        ChooseDelete(cut);

        cut.Find("dialog button.btn-danger").Click();

        cut.WaitForAssertion(() => RecipeExists(id).ShouldBeFalse());
        NavigatedTo.ShouldBe("http://localhost/");
    }

    [Fact]
    public void Delete_Cancelled_KeepsTheRecipe()
    {
        var id = SeedPancakes();
        var cut = RenderDetail(id);
        ChooseDelete(cut);

        cut.Find("dialog button[autofocus]").Click();

        cut.WaitForAssertion(() => JSInterop.VerifyInvoke("appDialog.close"));
        RecipeExists(id).ShouldBeTrue();
    }

    [Fact]
    public void MenuOffersEdit()
    {
        var id = SeedPancakes();
        var cut = RenderDetail(id);

        cut.Find("button[aria-label='Recipe actions']").Click();

        cut.Find("a.overflow-menu-item").GetAttribute("href").ShouldBe($"recipe/{id}/edit");
    }

    [Fact]
    public void Ingredients_ShowTheUnitTheyWereEnteredIn()
    {
        var id = Db.SeedRecipe("Curry", new (string, decimal?, string?, string?)[]
        {
            ("Curry powder", 2m, "Tablespoons", null),
            ("Rice", 1.5m, "Cups", null),
            ("Stock", 1m, "Cups", null),
        });

        var cut = RenderDetail(id);

        cut.FindAll(".ingredient-check span").Select(s => s.TextContent.Trim())
            .ShouldBe(new[] { "2 tbsp Curry powder", "1.5 cups Rice", "1 cup Stock" });
    }

    [Fact]
    public void Ingredients_ShowAReadableAmount_WithNoUnitForCounts()
    {
        var id = SeedPancakes();

        var cut = RenderDetail(id);

        cut.FindAll(".ingredient-check span").Select(s => s.TextContent.Trim())
            .ShouldBe(new[] { "200 g Flour", "2 Eggs" });
    }

    [Fact]
    public void TickingAnIngredient_CrossesItOff_AndKeepsItForThisTab()
    {
        var id = SeedPancakes();
        var cut = RenderDetail(id);

        cut.FindAll(".ingredient-check input")[0].Change(true);

        cut.WaitForAssertion(() => HasDraft(CookingProgress.KeyFor(id), DraftLifetime.Tab).ShouldBeTrue());
        cut.FindAll(".ingredient-check")[0].ClassList.ShouldContain("done");
        cut.FindAll(".ingredient-check")[1].ClassList.ShouldNotContain("done");
    }

    [Fact]
    public async Task SavedProgress_IsRestored()
    {
        var modified = new DateTime(2026, 10, 1, 12, 0, 0);
        var id = SeedPancakes(modified);
        var eggs = LineIds(id)[1];
        await Drafts.SaveAsync(CookingProgress.KeyFor(id), new CookingProgress(modified, new List<int> { eggs }), DraftLifetime.Tab);

        var cut = RenderDetail(id);

        cut.WaitForAssertion(() => cut.FindAll(".ingredient-check")[1].ClassList.ShouldContain("done"));
        cut.FindAll(".ingredient-check")[0].ClassList.ShouldNotContain("done");
    }

    [Fact]
    public async Task ProgressFromBeforeAnEdit_IsDiscarded()
    {
        var modified = new DateTime(2026, 10, 1, 12, 0, 0);
        var id = SeedPancakes(modified);
        var eggs = LineIds(id)[1];
        await Drafts.SaveAsync(CookingProgress.KeyFor(id),
            new CookingProgress(modified.AddDays(-1), new List<int> { eggs }), DraftLifetime.Tab);

        var cut = RenderDetail(id);

        cut.WaitForAssertion(() => HasDraft(CookingProgress.KeyFor(id), DraftLifetime.Tab).ShouldBeFalse());
        cut.FindAll(".ingredient-check.done").ShouldBeEmpty();
    }

    [Fact]
    public void UncheckAll_ClearsTheTicksAndTheSavedProgress()
    {
        var id = SeedPancakes();
        var cut = RenderDetail(id);
        cut.FindAll(".ingredient-check input")[0].Change(true);
        cut.WaitForAssertion(() => HasDraft(CookingProgress.KeyFor(id), DraftLifetime.Tab).ShouldBeTrue());

        cut.Find("button:contains('Uncheck all')").Click();

        cut.WaitForAssertion(() => HasDraft(CookingProgress.KeyFor(id), DraftLifetime.Tab).ShouldBeFalse());
        cut.FindAll(".ingredient-check.done").ShouldBeEmpty();
    }
}
