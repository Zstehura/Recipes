using RecipesApp.Shared;
using Shouldly;

namespace Recipes.Tests.Unit;

public class AppInfoTests
{
    [Fact]
    public void Title_PutsThePageFirst_ThenTheAppName()
    {
        AppInfo.Title("Grocery list").ShouldBe("Grocery list · Home Recipe Repo");
    }

    [Fact]
    public void ShortName_FitsUnderAHomeScreenIcon()
    {
        AppInfo.ShortName.Length.ShouldBeLessThanOrEqualTo(12);
    }
}
