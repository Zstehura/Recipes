using Microsoft.Extensions.Logging.Abstractions;
using Recipes.Tests.TestData;
using RecipesApp.Services.Drafts;
using Shouldly;

namespace Recipes.Tests.Unit;

public class DraftAutosaveTests
{
    private readonly InMemoryDraftStore _store = new();
    private readonly DraftService _drafts;
    private readonly DraftAutosave<FormState> _autosave;

    public DraftAutosaveTests()
    {
        _drafts = new DraftService(_store, new ManualTimeProvider(), NullLogger<DraftService>.Instance);
        _autosave = new DraftAutosave<FormState>(_drafts, "form", DraftLifetime.Device);
    }

    private sealed record FormState(string Name, int Servings);

    private static readonly FormState Empty = new("", 1);

    [Fact]
    public async Task SaveIfChanged_BeforeStart_DoesNothing()
    {
        await _autosave.SaveIfChangedAsync(new FormState("Soup", 4));

        _store.WriteCount.ShouldBe(0);
    }

    [Fact]
    public async Task SaveIfChanged_StateUnchanged_DoesNotWrite()
    {
        _autosave.Start(Empty);

        await _autosave.SaveIfChangedAsync(Empty);

        _store.WriteCount.ShouldBe(0);
        _store.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveIfChanged_StateChanged_SavesDraftOnce()
    {
        _autosave.Start(Empty);
        var edited = new FormState("Soup", 4);

        await _autosave.SaveIfChangedAsync(edited);
        await _autosave.SaveIfChangedAsync(edited);   // re-render with no further change

        _store.WriteCount.ShouldBe(1);
        (await _drafts.RestoreAsync<FormState>("form", DraftLifetime.Device)).ShouldNotBeNull().Value.ShouldBe(edited);
    }

    [Fact]
    public async Task SaveIfChanged_EachNewChange_UpdatesDraft()
    {
        _autosave.Start(Empty);

        await _autosave.SaveIfChangedAsync(new FormState("S", 1));
        await _autosave.SaveIfChangedAsync(new FormState("Soup", 1));

        _store.WriteCount.ShouldBe(2);
        (await _drafts.RestoreAsync<FormState>("form", DraftLifetime.Device))!.Value.Name.ShouldBe("Soup");
    }

    [Fact]
    public async Task SaveIfChanged_BackToStartingState_DeletesDraft()
    {
        _autosave.Start(Empty);
        await _autosave.SaveIfChangedAsync(new FormState("Soup", 4));

        await _autosave.SaveIfChangedAsync(Empty);

        _store.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task Clear_DeletesDraftAndStopsTracking()
    {
        _autosave.Start(Empty);
        await _autosave.SaveIfChangedAsync(new FormState("Soup", 4));

        await _autosave.ClearAsync();
        await _autosave.SaveIfChangedAsync(new FormState("Stew", 2));   // e.g. a render while navigating away

        _autosave.IsActive.ShouldBeFalse();
        _store.Values.ShouldBeEmpty();
    }
}
