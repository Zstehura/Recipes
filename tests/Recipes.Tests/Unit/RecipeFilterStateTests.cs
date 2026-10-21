using RecipesApp.Shared;
using Shouldly;

namespace Recipes.Tests.Unit;

public class RecipeFilterStateTests
{
    [Fact]
    public void New_HasNoActiveFilters()
    {
        var filters = new RecipeFilterState();

        filters.ActiveCount.ShouldBe(0);
        filters.IsEmpty.ShouldBeTrue();
    }

    [Theory]
    [InlineData(10, null)]
    [InlineData(null, 60)]
    [InlineData(10, 60)]
    public void ActiveCount_CookingTimeRange_CountsOnceWhicheverEndsAreSet(int? min, int? max)
    {
        new RecipeFilterState { MinCookTime = min, MaxCookTime = max }.ActiveCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(2, null)]
    [InlineData(null, 6)]
    [InlineData(2, 6)]
    public void ActiveCount_ServingsRange_CountsOnceWhicheverEndsAreSet(int? min, int? max)
    {
        new RecipeFilterState { MinServings = min, MaxServings = max }.ActiveCount.ShouldBe(1);
    }

    [Fact]
    public void ActiveCount_EachTagCounts()
    {
        var filters = new RecipeFilterState { MaxCookTime = 30, MinServings = 2, Tags = { "Dinner", "Quick" } };

        filters.ActiveCount.ShouldBe(4);
        filters.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void ToggleTag_AddsThenRemoves()
    {
        var filters = new RecipeFilterState();

        filters.ToggleTag("Dinner");
        filters.Tags.ShouldBe(new[] { "Dinner" });

        filters.ToggleTag("Dinner");
        filters.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void Clear_ResetsEverything()
    {
        var filters = new RecipeFilterState
        {
            MinCookTime = 10,
            MaxCookTime = 60,
            MinServings = 2,
            MaxServings = 6,
            Tags = { "Dinner" },
        };

        filters.Clear();

        filters.IsEmpty.ShouldBeTrue();
        filters.MinCookTime.ShouldBeNull();
        filters.MaxCookTime.ShouldBeNull();
        filters.MinServings.ShouldBeNull();
        filters.MaxServings.ShouldBeNull();
    }
}
