using System.Text.Json;

namespace RecipesApp.Services.Drafts;

/// <summary>
/// Keeps a page's draft in step with its state: call <see cref="SaveIfChangedAsync"/> after each
/// render. It writes only when the state actually changed, and deletes the draft when the state
/// is back to where the page started, so merely opening a page never creates a draft.
/// </summary>
public sealed class DraftAutosave<T>
{
    private readonly DraftService _drafts;
    private readonly string _key;
    private readonly DraftLifetime _lifetime;
    private string? _baselineJson;
    private string? _lastJson;

    public DraftAutosave(DraftService drafts, string key, DraftLifetime lifetime)
    {
        _drafts = drafts;
        _key = key;
        _lifetime = lifetime;
    }

    public string Key => _key;

    /// <summary>False until <see cref="Start"/> is called, and again after <see cref="ClearAsync"/>.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Records the state the page started from (before any draft was applied) and starts
    /// tracking changes. Call it once the page has loaded and any draft has been restored.
    /// </summary>
    public void Start(T baseline)
    {
        _baselineJson = JsonSerializer.Serialize(baseline);
        _lastJson = _baselineJson;
        IsActive = true;
    }

    public async Task SaveIfChangedAsync(T current)
    {
        if (!IsActive)
            return;

        var json = JsonSerializer.Serialize(current);
        if (json == _lastJson)
            return;

        _lastJson = json;
        if (json == _baselineJson)
        {
            await _drafts.DiscardAsync(_key, _lifetime);
        }
        else
        {
            await _drafts.SaveAsync(_key, current, _lifetime);
        }
    }

    /// <summary>Deletes the draft and stops tracking, e.g. after the work was saved for real.</summary>
    public async Task ClearAsync()
    {
        IsActive = false;
        await _drafts.DiscardAsync(_key, _lifetime);
    }
}
