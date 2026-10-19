using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Recipes.Tests.Data;
using Recipes.Tests.TestData;
using RecipesApp.Services;
using RecipesApp.Services.Drafts;

namespace Recipes.Tests.Components;

/// <summary>
/// Shared setup for bUnit page tests: a real SQLite test database behind the real services, and
/// an in-memory draft store with a manual clock in place of the browser storage.
/// </summary>
public abstract class ComponentTestBase : BunitContext
{
    protected SqliteTestDatabase Db { get; } = new();
    protected InMemoryDraftStore Store { get; } = new();
    protected ManualTimeProvider Clock { get; } = new();
    protected DraftService Drafts { get; }

    protected ComponentTestBase()
    {
        Drafts = new DraftService(Store, Clock, NullLogger<DraftService>.Instance);
        Services.AddSingleton<TimeProvider>(Clock);
        Services.AddSingleton(Drafts);
        Services.AddSingleton(_ => new RecipeService(Db.CreateContext(), NullLogger<RecipeService>.Instance));
        Services.AddSingleton<IRecipeReader>(sp => sp.GetRequiredService<RecipeService>());
        Services.AddSingleton<GroceryListService>();
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
    }

    protected string NavigatedTo => Services.GetRequiredService<NavigationManager>().Uri;

    protected bool HasDraft(string key, DraftLifetime lifetime = DraftLifetime.Device) =>
        Store.Values.ContainsKey((lifetime, key));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Db.Dispose();
        }
        base.Dispose(disposing);
    }
}
