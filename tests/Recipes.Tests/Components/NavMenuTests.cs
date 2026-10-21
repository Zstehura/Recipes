using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RecipesApp.Shared;
using Shouldly;

namespace Recipes.Tests.Components;

public class NavMenuTests : BunitContext
{
    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    // Labels of the links marked as the current page, in the header nav and the bottom nav
    private static (string[] Top, string[] Bottom) CurrentLinks(IRenderedComponent<NavMenu> nav) => (
        nav.FindAll(".top-nav a[aria-current=page]").Select(a => a.TextContent.Trim()).ToArray(),
        nav.FindAll(".bottom-nav a[aria-current=page]").Select(a => a.TextContent.Trim()).ToArray());

    [Fact]
    public void RendersRealLinksForEveryPage()
    {
        var nav = Render<NavMenu>();

        nav.FindAll(".top-nav a").Select(a => a.GetAttribute("href"))
            .ShouldBe(new[] { "", "search-by-ingredients", "grocery-list", "ingredients", "import-export" });
        nav.FindAll(".bottom-nav a").Select(a => a.GetAttribute("href"))
            .ShouldBe(new[] { "", "search-by-ingredients", "grocery-list", "more" });
    }

    [Fact]
    public void RecipePage_MarksRecipesAsCurrent()
    {
        Navigation.NavigateTo("recipe/3/edit");

        var (top, bottom) = CurrentLinks(Render<NavMenu>());

        top.ShouldBe(new[] { "Recipes" });
        bottom.ShouldBe(new[] { "Recipes" });
    }

    [Fact]
    public void PageBehindMore_MarksItsOwnLinkInTheHeaderAndMoreInTheBottomNav()
    {
        Navigation.NavigateTo("import-export");

        var (top, bottom) = CurrentLinks(Render<NavMenu>());

        top.ShouldBe(new[] { "Import/Export" });
        bottom.ShouldBe(new[] { "More" });
    }

    [Fact]
    public void Navigating_MovesTheCurrentMarker()
    {
        var nav = Render<NavMenu>();
        CurrentLinks(nav).Bottom.ShouldBe(new[] { "Recipes" });

        Navigation.NavigateTo("grocery-list");

        nav.WaitForAssertion(() => CurrentLinks(nav).Bottom.ShouldBe(new[] { "Grocery" }));
        CurrentLinks(nav).Top.ShouldBe(new[] { "Grocery list" });
    }
}
