using RecipesApp.Services.Drafts;

namespace Recipes.Tests.TestData;

/// <summary>An <see cref="IDraftStore"/> backed by a dictionary, with an optional failure to simulate browser storage errors.</summary>
public sealed class InMemoryDraftStore : IDraftStore
{
    public Dictionary<(DraftLifetime Lifetime, string Key), string> Values { get; } = new();

    public int WriteCount { get; private set; }

    public int DeleteCount { get; private set; }

    /// <summary>When set, every call throws this exception.</summary>
    public Exception? FailWith { get; set; }

    public Task<string?> GetAsync(string key, DraftLifetime lifetime)
    {
        ThrowIfFailing();
        return Task.FromResult(Values.TryGetValue((lifetime, key), out var value) ? value : null);
    }

    public Task SetAsync(string key, string value, DraftLifetime lifetime)
    {
        ThrowIfFailing();
        WriteCount++;
        Values[(lifetime, key)] = value;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, DraftLifetime lifetime)
    {
        ThrowIfFailing();
        DeleteCount++;
        Values.Remove((lifetime, key));
        return Task.CompletedTask;
    }

    private void ThrowIfFailing()
    {
        if (FailWith != null)
            throw FailWith;
    }
}
