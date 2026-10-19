using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Recipes.Tests.Data;
using Recipes.Tests.TestData;
using RecipesApp.Models;
using RecipesApp.Pages;
using RecipesApp.Services;
using RecipesApp.Services.Drafts;
using Shouldly;

namespace Recipes.Tests.Components;

/// <summary>
/// Renders the real recipe form (bUnit) against a real SQLite database, with the browser storage
/// replaced by an in-memory draft store, to check that drafts are restored, saved, and cleared.
/// </summary>
public sealed class RecipeFormDraftTests : BunitContext
{
    private readonly SqliteTestDatabase _db = new();
    private readonly InMemoryDraftStore _store = new();
    private readonly ManualTimeProvider _clock = new();
    private readonly DraftService _drafts;

    public RecipeFormDraftTests()
    {
        _drafts = new DraftService(_store, _clock, NullLogger<DraftService>.Instance);
        Services.AddSingleton<TimeProvider>(_clock);
        Services.AddSingleton(_drafts);
        Services.AddSingleton(_ => new RecipeService(_db.CreateContext(), NullLogger<RecipeService>.Instance));
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _db.Dispose();
        }
        base.Dispose(disposing);
    }

    private static RecipeDraft CompleteDraft(DateTime? baseModifiedDate = null) => new(
        Name: "Tomato soup",
        Instructions: "Simmer for 20 minutes.",
        CookingTime: 30,
        Servings: 4,
        Tags: "Soup",
        Notes: "",
        Ingredients: new List<IngredientEntry> { new() { Name = "Tomatoes", Quantity = 6, Unit = MeasurementUnit.Pieces } },
        BaseModifiedDate: baseModifiedDate);

    private Task SeedDraftAsync(int recipeId, RecipeDraft draft) =>
        _drafts.SaveAsync(RecipeDraft.KeyFor(recipeId), draft, DraftLifetime.Device);

    private bool HasDraft(int recipeId) =>
        _store.Values.ContainsKey((DraftLifetime.Device, RecipeDraft.KeyFor(recipeId)));

    private DateTime StoredModifiedDate(int recipeId)
    {
        using var context = _db.CreateContext();
        return context.Recipes.AsNoTracking().Single(r => r.Id == recipeId).ModifiedDate;
    }

    private string NavigatedTo => Services.GetRequiredService<NavigationManager>().Uri;

    [Fact]
    public void NewRecipe_OpenedWithoutChanges_CreatesNoDraft()
    {
        Render<RecipeForm>();

        _store.WriteCount.ShouldBe(0);
    }

    [Fact]
    public void NewRecipe_TypingAName_SavesDraft()
    {
        var cut = Render<RecipeForm>();

        cut.Find("#name").Change("Tomato soup");

        cut.WaitForAssertion(() => HasDraft(0).ShouldBeTrue());
    }

    [Fact]
    public async Task NewRecipe_WithSavedDraft_RestoresFormAndShowsNotice()
    {
        await SeedDraftAsync(0, CompleteDraft());
        _clock.Advance(TimeSpan.FromMinutes(5));

        var cut = Render<RecipeForm>();

        cut.WaitForAssertion(() => cut.Find("#name").GetAttribute("value").ShouldBe("Tomato soup"));
        cut.Find("#instructions").GetAttribute("value").ShouldBe("Simmer for 20 minutes.");
        cut.Markup.ShouldContain("Restored your unsaved changes from 5 minutes ago.");
    }

    [Fact]
    public async Task NewRecipe_WithCorruptDraft_ShowsEmptyFormAndDeletesDraft()
    {
        _store.Values[(DraftLifetime.Device, RecipeDraft.KeyFor(0))] = "{ not valid json";

        var cut = Render<RecipeForm>();

        cut.WaitForAssertion(() => HasDraft(0).ShouldBeFalse());
        cut.Find("#name").GetAttribute("value").ShouldBeNullOrEmpty();
        cut.Markup.ShouldNotContain("Restored your unsaved changes");
    }

    [Fact]
    public async Task EditRecipe_DraftFromCurrentVersion_IsRestored()
    {
        var id = _db.SeedRecipe("Bread", "Flour");
        await SeedDraftAsync(id, CompleteDraft(baseModifiedDate: StoredModifiedDate(id)) with { Name = "Sourdough" });

        var cut = Render<RecipeForm>(p => p.Add(f => f.Id, id));

        cut.WaitForAssertion(() => cut.Find("#name").GetAttribute("value").ShouldBe("Sourdough"));
    }

    [Fact]
    public async Task EditRecipe_DraftFromOlderVersion_IsDiscardedWithWarning()
    {
        var id = _db.SeedRecipe("Bread", "Flour");
        var olderVersion = StoredModifiedDate(id).AddDays(-1);
        await SeedDraftAsync(id, CompleteDraft(baseModifiedDate: olderVersion) with { Name = "Sourdough" });

        var cut = Render<RecipeForm>(p => p.Add(f => f.Id, id));

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("discarded because it has been changed since"));
        cut.Find("#name").GetAttribute("value").ShouldBe("Bread");
        HasDraft(id).ShouldBeFalse();
    }

    [Fact]
    public async Task SavingTheRecipe_ClearsDraftAndGoesHome()
    {
        await SeedDraftAsync(0, CompleteDraft());
        var cut = Render<RecipeForm>();
        cut.WaitForAssertion(() => cut.Find("#name").GetAttribute("value").ShouldBe("Tomato soup"));

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => HasDraft(0).ShouldBeFalse());
        NavigatedTo.ShouldBe("http://localhost/");
        using var context = _db.CreateContext();
        context.Recipes.Single().Name.ShouldBe("Tomato soup");
    }

    [Fact]
    public async Task Cancel_ClearsDraft()
    {
        await SeedDraftAsync(0, CompleteDraft());
        var cut = Render<RecipeForm>();
        cut.WaitForAssertion(() => cut.Find("#name").GetAttribute("value").ShouldBe("Tomato soup"));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Cancel").Click();

        cut.WaitForAssertion(() => HasDraft(0).ShouldBeFalse());
        NavigatedTo.ShouldBe("http://localhost/");
    }

    [Fact]
    public async Task DiscardChanges_DeletesDraft()
    {
        await SeedDraftAsync(0, CompleteDraft());
        var cut = Render<RecipeForm>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Restored your unsaved changes"));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Discard changes").Click();

        cut.WaitForAssertion(() => HasDraft(0).ShouldBeFalse());
    }
}
