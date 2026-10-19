namespace RecipesApp.Services.Drafts;

/// <summary>
/// The storage boundary for drafts: string values by key. The app uses encrypted browser
/// storage (<see cref="ProtectedBrowserDraftStore"/>); tests use an in-memory fake, so all draft
/// logic in <see cref="DraftService"/> can be tested without a browser.
/// </summary>
public interface IDraftStore
{
    Task<string?> GetAsync(string key, DraftLifetime lifetime);

    Task SetAsync(string key, string value, DraftLifetime lifetime);

    Task DeleteAsync(string key, DraftLifetime lifetime);
}
