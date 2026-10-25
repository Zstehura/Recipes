namespace RecipesApp.Shared;

/// <summary>
/// The app's name, in one place. The web app manifest (wwwroot/manifest.webmanifest) is a static
/// file that can't read this, so a test checks that it matches.
/// </summary>
public static class AppInfo
{
    public const string Name = "Home Recipe Repo";

    /// <summary>Used under the home-screen icon, where about 12 characters fit.</summary>
    public const string ShortName = "Recipe Repo";

    /// <summary>The browser tab title for a page, e.g. "Grocery list · Home Recipe Repo".</summary>
    public static string Title(string page) => $"{page} · {Name}";
}
