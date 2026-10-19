using RecipesApp.Services;
using Shouldly;

namespace Recipes.Tests.Unit;

public class IngredientNameTests
{
    [Theory]
    [InlineData("flour", "Flour")]
    [InlineData("  flour  ", "Flour")]
    [InlineData("Flour", "Flour")]
    [InlineData("brown sugar", "Brown sugar")]
    [InlineData("eGGS", "EGGS")]
    [InlineData("x", "X")]
    [InlineData("éclair", "Éclair")]
    [InlineData("1% milk", "1% milk")]
    public void NormalizeName_Text_TrimsAndCapitalizesFirstLetterOnly(string input, string expected)
    {
        IngredientService.NormalizeName(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeName_NullOrWhitespace_ReturnsInputUnchanged(string? input)
    {
        IngredientService.NormalizeName(input!).ShouldBe(input);
    }
}
