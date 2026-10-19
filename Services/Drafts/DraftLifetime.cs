namespace RecipesApp.Services.Drafts;

/// <summary>How long a draft is kept in the browser.</summary>
public enum DraftLifetime
{
    /// <summary>
    /// Kept on this device (browser local storage) until saved, discarded, or expired.
    /// For work the user would be upset to lose, such as a half-written recipe.
    /// </summary>
    Device,

    /// <summary>
    /// Kept only for this browser tab (session storage); survives reloads, cleared when the tab
    /// is closed. For view state such as search filters, which shouldn't linger for days.
    /// </summary>
    Tab,
}
