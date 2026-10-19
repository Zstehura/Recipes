using RecipesApp.Models;
using RecipesApp.Services;
using Shouldly;

namespace Recipes.Tests.Unit;

public class IngredientEntryMapperTests
{
    private static RecipeIngredient Line(decimal? quantity, string? unit, string? modifier = null) => new()
    {
        Ingredient = new Ingredient { Name = "Flour" },
        Quantity = quantity,
        Unit = unit,
        Modifier = modifier,
    };

    [Theory]
    [InlineData(250, "g", 250, MeasurementUnit.Grams)]
    [InlineData(1500, "g", 1.5, MeasurementUnit.Kilograms)]
    [InlineData(473.176, "ml", 473.176, MeasurementUnit.Milliliters)]
    [InlineData(2000, "ml", 2, MeasurementUnit.Liters)]
    [InlineData(3, "pieces", 3, MeasurementUnit.Pieces)]
    public void FromRecipeIngredient_WithQuantity_UsesReadableDisplayUnit(
        double quantity, string baseUnit, double expectedQuantity, MeasurementUnit expectedUnit)
    {
        var entry = IngredientEntryMapper.FromRecipeIngredient(Line((decimal)quantity, baseUnit));

        entry.Name.ShouldBe("Flour");
        entry.Quantity.ShouldBe((decimal)expectedQuantity);
        entry.Unit.ShouldBe(expectedUnit);
    }

    [Fact]
    public void FromRecipeIngredient_NoQuantity_KeepsQuantityNull()
    {
        // Bug #6: editing a recipe used to turn "salt (to taste)" into 0 salt.
        var entry = IngredientEntryMapper.FromRecipeIngredient(Line(null, null, "to taste"));

        entry.Quantity.ShouldBeNull();
        entry.Unit.ShouldBe(MeasurementUnit.Pieces);
        entry.Modifier.ShouldBe("to taste");
    }

    [Fact]
    public void FromRecipeIngredient_NoModifier_UsesEmptyString()
    {
        IngredientEntryMapper.FromRecipeIngredient(Line(1, "g")).Modifier.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(250, "g")]
    [InlineData(1500, "g")]
    [InlineData(473.176, "ml")]
    [InlineData(3, "pieces")]
    public void FromRecipeIngredient_ThenConvertBack_GivesOriginalBaseQuantity(double quantity, string baseUnit)
    {
        // RecipeService converts entries back to base units when saving, so editing and
        // saving a recipe without changes must not alter what's stored.
        var entry = IngredientEntryMapper.FromRecipeIngredient(Line((decimal)quantity, baseUnit));

        var (savedQuantity, savedUnit) = UnitConversionService.ConvertToBaseUnit(entry.Quantity!.Value, entry.Unit);

        savedQuantity.ShouldBe((decimal)quantity);
        savedUnit.ShouldBe(baseUnit);
    }
}
