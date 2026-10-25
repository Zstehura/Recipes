using RecipesApp.Models;
using RecipesApp.Services;

namespace Recipes.Tests.TestData;

/// <summary>
/// A recipe reader that doesn't answer until the test calls <see cref="Release"/>. The real one
/// (SQLite) always finishes at once, which hides bugs in pages that assume loading is done by
/// the time they first render; this lets a test render the page mid-load.
/// </summary>
public sealed class GatedRecipeReader(IRecipeReader inner) : IRecipeReader
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<Recipe?> GetRecipeByIdAsync(int id)
    {
        await _gate.Task;
        return await inner.GetRecipeByIdAsync(id);
    }

    public void Release() => _gate.TrySetResult();
}
