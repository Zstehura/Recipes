using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace RecipesApp.Services.Drafts;

/// <summary>
/// Stores drafts in the browser using ASP.NET Core's protected browser storage. Values are
/// encrypted and signed with the app's data protection keys before they reach the browser, so
/// they can't be read or tampered with client-side. Calls go through JavaScript interop, so
/// they only work once the page is interactive (not during prerendering).
/// </summary>
public sealed class ProtectedBrowserDraftStore : IDraftStore
{
    private readonly ProtectedLocalStorage _localStorage;
    private readonly ProtectedSessionStorage _sessionStorage;

    public ProtectedBrowserDraftStore(ProtectedLocalStorage localStorage, ProtectedSessionStorage sessionStorage)
    {
        _localStorage = localStorage;
        _sessionStorage = sessionStorage;
    }

    public async Task<string?> GetAsync(string key, DraftLifetime lifetime)
    {
        var result = await StorageFor(lifetime).GetAsync<string>(key);
        return result.Success ? result.Value : null;
    }

    public async Task SetAsync(string key, string value, DraftLifetime lifetime) =>
        await StorageFor(lifetime).SetAsync(key, value);

    public async Task DeleteAsync(string key, DraftLifetime lifetime) =>
        await StorageFor(lifetime).DeleteAsync(key);

    private ProtectedBrowserStorage StorageFor(DraftLifetime lifetime) =>
        lifetime == DraftLifetime.Tab ? _sessionStorage : _localStorage;
}
