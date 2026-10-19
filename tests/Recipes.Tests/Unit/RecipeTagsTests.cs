using RecipesApp.Services;
using Shouldly;

namespace Recipes.Tests.Unit;

public class RecipeTagsTests
{
    [Theory]
    [InlineData("Italian,Dinner", new[] { "Italian", "Dinner" })]
    [InlineData("Italian, Dinner", new[] { "Italian", "Dinner" })]
    [InlineData("  Italian ,  Dinner  ", new[] { "Italian", "Dinner" })]
    [InlineData("Quick Meals,Dinner", new[] { "Quick Meals", "Dinner" })]
    [InlineData("Italian,,Dinner,", new[] { "Italian", "Dinner" })]
    [InlineData("Solo", new[] { "Solo" })]
    public void Parse_CommaSeparatedTags_ReturnsTrimmedNonEmptyTags(string tags, string[] expected)
    {
        RecipeTags.Parse(tags).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(" , ,")]
    public void Parse_NoTags_ReturnsEmpty(string? tags)
    {
        RecipeTags.Parse(tags).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Italian, Dinner", "dinner", true)]
    [InlineData("Italian, Dinner", " DINNER ", true)]
    [InlineData("Italian, Dinner", "Ital", false)]          // whole tags only
    [InlineData("Quick Meals", "Quick", false)]
    [InlineData("Quick Meals", "quick meals", true)]
    [InlineData("", "Dinner", false)]
    public void ContainsAny_SingleWantedTag_MatchesWholeTagsIgnoringCase(string tags, string wanted, bool expected)
    {
        RecipeTags.ContainsAny(tags, new[] { wanted }).ShouldBe(expected);
    }

    [Fact]
    public void ContainsAny_SeveralWantedTags_MatchesIfAnyMatch()
    {
        RecipeTags.ContainsAny("Breakfast,Sweet", new[] { "Dinner", "sweet" }).ShouldBeTrue();
    }
}
