using RecipesApp.Models;
using RecipesApp.Services;
using Shouldly;

namespace Recipes.Tests.Unit;

public class IngredientEntryMapperTests
{
    private static RecipeIngredient Line(decimal? quantity, MeasurementUnit? unit, string? modifier = null) => new()
    {
        Ingredient = new Ingredient { Name = "Flour" },
        Quantity = quantity,
        Unit = unit,
        Modifier = modifier,
    };

    [Theory]
    [InlineData(2, MeasurementUnit.Tablespoons)]
    [InlineData(1.5, MeasurementUnit.Cups)]
    [InlineData(1500, MeasurementUnit.Grams)]
    [InlineData(0.125, MeasurementUnit.Cups)]
    [InlineData(3, MeasurementUnit.Pieces)]
    public void FromRecipeIngredient_KeepsTheUnitItWasEnteredIn(double quantity, MeasurementUnit unit)
    {
        var entry = IngredientEntryMapper.FromRecipeIngredient(Line((decimal)quantity, unit));

        entry.Name.ShouldBe("Flour");
        entry.Quantity.ShouldBe((decimal)quantity);
        entry.Unit.ShouldBe(unit);
    }

    [Fact]
    public void FromRecipeIngredient_DropsTrailingZeros()
    {
        // SQLite hands decimals back as e.g. 600.0, which the edit form showed as "600.0"
        var entry = IngredientEntryMapper.FromRecipeIngredient(Line(600.0000m, MeasurementUnit.Grams));

        entry.Quantity!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture).ShouldBe("600");
    }

    [Theory]
    [InlineData(14.7868, MeasurementUnit.Milliliters, "14.79")]   // 1 tbsp saved as ml by an older version
    [InlineData(28.3495, MeasurementUnit.Grams, "28.35")]         // 1 oz saved as g
    [InlineData(0.3333, MeasurementUnit.Cups, "0.3333")]          // a third of a cup keeps its precision
    [InlineData(1.5, MeasurementUnit.Teaspoons, "1.5")]
    public void FromRecipeIngredient_RoundsOnlyGramsAndMilliliters(double quantity, MeasurementUnit unit, string expected)
    {
        var entry = IngredientEntryMapper.FromRecipeIngredient(Line((decimal)quantity, unit));

        entry.Quantity!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture).ShouldBe(expected);
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
        IngredientEntryMapper.FromRecipeIngredient(Line(1, MeasurementUnit.Grams)).Modifier.ShouldBe(string.Empty);
    }
}
