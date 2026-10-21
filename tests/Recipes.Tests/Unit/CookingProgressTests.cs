using RecipesApp.Models;
using RecipesApp.Services.Drafts;
using Shouldly;

namespace Recipes.Tests.Unit;

public class CookingProgressTests
{
    private static readonly DateTime Modified = new(2026, 10, 1, 12, 0, 0);

    [Fact]
    public void KeyFor_IsPerRecipe()
    {
        CookingProgress.KeyFor(5).ShouldBe("cooking:5");
        CookingProgress.KeyFor(5).ShouldNotBe(CookingProgress.KeyFor(6));
    }

    [Fact]
    public void Capture_SortsIds_SoTheSameTicksAlwaysLookTheSame()
    {
        var recipe = new Recipe { ModifiedDate = Modified };

        var progress = CookingProgress.Capture(recipe, new[] { 9, 3, 7 });

        progress.DoneIngredientIds.ShouldBe(new[] { 3, 7, 9 });
        progress.RecipeModifiedDate.ShouldBe(Modified);
    }

    [Fact]
    public void IsStaleFor_SameRecipeVersion_IsFalse()
    {
        var progress = new CookingProgress(Modified, new List<int> { 1 });

        progress.IsStaleFor(new Recipe { ModifiedDate = Modified }).ShouldBeFalse();
    }

    [Fact]
    public void IsStaleFor_RecipeEditedSince_IsTrue()
    {
        var progress = new CookingProgress(Modified, new List<int> { 1 });

        progress.IsStaleFor(new Recipe { ModifiedDate = Modified.AddMinutes(5) }).ShouldBeTrue();
    }
}
