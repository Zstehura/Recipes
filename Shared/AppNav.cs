namespace RecipesApp.Shared;

/// <summary>The parts of the app the navigation links to.</summary>
public enum NavSection
{
    None,
    Recipes,
    Find,
    Grocery,
    Ingredients,
    ImportExport,
    More,
}

/// <summary>
/// Works out which navigation item a page belongs to, so the nav can highlight it. Kept separate
/// from the component because it's plain string logic, and it's tested on its own.
/// </summary>
public static class AppNav
{
    /// <param name="relativePath">The path relative to the app's base, as returned by
    /// <c>NavigationManager.ToBaseRelativePath</c> (no leading slash; may include a query).</param>
    public static NavSection SectionFor(string relativePath)
    {
        var path = relativePath.Split('?', '#')[0].Trim('/');
        var first = path.Split('/')[0].ToLowerInvariant();

        return first switch
        {
            "" or "recipe" => NavSection.Recipes,   // the list, a recipe, and the new/edit form
            "search-by-ingredients" => NavSection.Find,
            "grocery-list" => NavSection.Grocery,
            "ingredients" => NavSection.Ingredients,
            "import-export" => NavSection.ImportExport,
            "more" => NavSection.More,
            _ => NavSection.None,
        };
    }

    /// <summary>The phone nav has no room for every page, so these share its "More" item.</summary>
    public static bool IsUnderMore(NavSection section) =>
        section is NavSection.More or NavSection.Ingredients or NavSection.ImportExport;
}
