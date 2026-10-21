using RecipesApp.Shared;
using Shouldly;

namespace Recipes.Tests.Unit;

public class AppNavTests
{
    [Theory]
    [InlineData("", NavSection.Recipes)]
    [InlineData("recipe/new", NavSection.Recipes)]
    [InlineData("recipe/12", NavSection.Recipes)]
    [InlineData("recipe/12/edit", NavSection.Recipes)]
    [InlineData("search-by-ingredients", NavSection.Find)]
    [InlineData("grocery-list", NavSection.Grocery)]
    [InlineData("ingredients", NavSection.Ingredients)]
    [InlineData("import-export", NavSection.ImportExport)]
    [InlineData("more", NavSection.More)]
    public void SectionFor_KnownPages_ReturnsTheirSection(string path, NavSection expected)
    {
        AppNav.SectionFor(path).ShouldBe(expected);
    }

    [Theory]
    [InlineData("grocery-list?x=1", NavSection.Grocery)]
    [InlineData("grocery-list#list", NavSection.Grocery)]
    [InlineData("?q=soup", NavSection.Recipes)]
    [InlineData("Grocery-List/", NavSection.Grocery)]
    [InlineData("/ingredients", NavSection.Ingredients)]
    public void SectionFor_IgnoresQueryFragmentCaseAndSlashes(string path, NavSection expected)
    {
        AppNav.SectionFor(path).ShouldBe(expected);
    }

    [Theory]
    [InlineData("recipes")]          // a prefix of "recipe" isn't enough; whole segments only
    [InlineData("grocery")]
    [InlineData("not-a-page")]
    public void SectionFor_UnknownPages_ReturnsNone(string path)
    {
        AppNav.SectionFor(path).ShouldBe(NavSection.None);
    }

    [Theory]
    [InlineData(NavSection.More, true)]
    [InlineData(NavSection.Ingredients, true)]
    [InlineData(NavSection.ImportExport, true)]
    [InlineData(NavSection.Recipes, false)]
    [InlineData(NavSection.Find, false)]
    [InlineData(NavSection.Grocery, false)]
    [InlineData(NavSection.None, false)]
    public void IsUnderMore_OnlyThePagesBehindTheMoreItem(NavSection section, bool expected)
    {
        AppNav.IsUnderMore(section).ShouldBe(expected);
    }
}
