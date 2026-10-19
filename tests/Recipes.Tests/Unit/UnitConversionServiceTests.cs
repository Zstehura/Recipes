using RecipesApp.Models;
using RecipesApp.Services;
using Shouldly;

namespace Recipes.Tests.Unit;

public class UnitConversionServiceTests
{
    [Theory]
    [InlineData(MeasurementUnit.Grams, 1, 1, "g")]
    [InlineData(MeasurementUnit.Kilograms, 1, 1000, "g")]
    [InlineData(MeasurementUnit.Ounces, 1, 28.3495, "g")]
    [InlineData(MeasurementUnit.Pounds, 1, 453.592, "g")]
    [InlineData(MeasurementUnit.Milliliters, 1, 1, "ml")]
    [InlineData(MeasurementUnit.Liters, 1, 1000, "ml")]
    [InlineData(MeasurementUnit.Cups, 1, 236.588, "ml")]
    [InlineData(MeasurementUnit.Tablespoons, 1, 14.7868, "ml")]
    [InlineData(MeasurementUnit.Teaspoons, 1, 4.92892, "ml")]
    [InlineData(MeasurementUnit.FluidOunces, 1, 29.5735, "ml")]
    [InlineData(MeasurementUnit.Pieces, 1, 1, "pieces")]
    [InlineData(MeasurementUnit.Cups, 2.5, 591.47, "ml")]
    [InlineData(MeasurementUnit.Pounds, 0.5, 226.796, "g")]
    public void ConvertToBaseUnit_KnownUnit_ReturnsBaseQuantityAndUnit(
        MeasurementUnit unit, double quantity, double expectedQuantity, string expectedUnit)
    {
        var (baseQuantity, baseUnit) = UnitConversionService.ConvertToBaseUnit((decimal)quantity, unit);

        baseQuantity.ShouldBe((decimal)expectedQuantity);
        baseUnit.ShouldBe(expectedUnit);
    }

    [Theory]
    [InlineData(MeasurementUnit.Kilograms)]
    [InlineData(MeasurementUnit.Cups)]
    [InlineData(MeasurementUnit.Pieces)]
    public void ConvertToBaseUnit_Zero_ReturnsZero(MeasurementUnit unit)
    {
        var (baseQuantity, _) = UnitConversionService.ConvertToBaseUnit(0m, unit);

        baseQuantity.ShouldBe(0m);
    }

    [Fact]
    public void ConvertToBaseUnit_NegativeQuantity_IsConvertedWithoutValidation()
    {
        // Documents current behavior: the converter does not reject negative values.
        // Validation, if any, has to happen before conversion.
        var (baseQuantity, baseUnit) = UnitConversionService.ConvertToBaseUnit(-2m, MeasurementUnit.Kilograms);

        baseQuantity.ShouldBe(-2000m);
        baseUnit.ShouldBe("g");
    }

    [Fact]
    public void ConvertToBaseUnit_UndefinedEnumValue_FallsBackToPieces()
    {
        var (baseQuantity, baseUnit) = UnitConversionService.ConvertToBaseUnit(3m, (MeasurementUnit)999);

        baseQuantity.ShouldBe(3m);
        baseUnit.ShouldBe(UnitConversionService.BASE_COUNT_UNIT);
    }

    [Theory]
    [InlineData(500, "g", 500, MeasurementUnit.Grams)]
    [InlineData(999.99, "g", 999.99, MeasurementUnit.Grams)]
    [InlineData(1000, "g", 1, MeasurementUnit.Kilograms)]
    [InlineData(2500, "g", 2.5, MeasurementUnit.Kilograms)]
    [InlineData(250, "ml", 250, MeasurementUnit.Milliliters)]
    [InlineData(1000, "ml", 1, MeasurementUnit.Liters)]
    [InlineData(1500, "ml", 1.5, MeasurementUnit.Liters)]
    [InlineData(3, "pieces", 3, MeasurementUnit.Pieces)]
    [InlineData(0, "g", 0, MeasurementUnit.Grams)]
    public void ConvertFromBaseUnit_KnownBaseUnit_ReturnsReadableUnit(
        double quantity, string baseUnit, double expectedQuantity, MeasurementUnit expectedUnit)
    {
        var (displayQuantity, displayUnit) = UnitConversionService.ConvertFromBaseUnit((decimal)quantity, baseUnit);

        displayQuantity.ShouldBe((decimal)expectedQuantity);
        displayUnit.ShouldBe(expectedUnit);
    }

    [Theory]
    [InlineData("cups")]
    [InlineData("G")]
    [InlineData("")]
    public void ConvertFromBaseUnit_UnknownBaseUnit_FallsBackToPieces(string baseUnit)
    {
        var (displayQuantity, displayUnit) = UnitConversionService.ConvertFromBaseUnit(7m, baseUnit);

        displayQuantity.ShouldBe(7m);
        displayUnit.ShouldBe(MeasurementUnit.Pieces);
    }

    [Theory]
    [InlineData(MeasurementUnit.Grams, 250)]
    [InlineData(MeasurementUnit.Kilograms, 2.5)]
    [InlineData(MeasurementUnit.Milliliters, 750)]
    [InlineData(MeasurementUnit.Liters, 1.25)]
    [InlineData(MeasurementUnit.Pieces, 4)]
    public void ConvertToBaseThenBack_MetricUnit_RoundTripsExactly(MeasurementUnit unit, double quantity)
    {
        var (baseQuantity, baseUnit) = UnitConversionService.ConvertToBaseUnit((decimal)quantity, unit);
        var (roundTripQuantity, roundTripUnit) = UnitConversionService.ConvertFromBaseUnit(baseQuantity, baseUnit);

        roundTripQuantity.ShouldBe((decimal)quantity);
        roundTripUnit.ShouldBe(unit);
    }

    [Fact]
    public void ConvertToBaseThenBack_ImperialUnit_ComesBackAsMetric()
    {
        // Only base units are stored, so the original imperial unit is not preserved.
        var (baseQuantity, baseUnit) = UnitConversionService.ConvertToBaseUnit(2m, MeasurementUnit.Cups);
        var (roundTripQuantity, roundTripUnit) = UnitConversionService.ConvertFromBaseUnit(baseQuantity, baseUnit);

        roundTripQuantity.ShouldBe(473.176m);
        roundTripUnit.ShouldBe(MeasurementUnit.Milliliters);
    }

    [Theory]
    [InlineData(MeasurementUnit.Grams, "g")]
    [InlineData(MeasurementUnit.Kilograms, "kg")]
    [InlineData(MeasurementUnit.Ounces, "oz")]
    [InlineData(MeasurementUnit.Pounds, "lb")]
    [InlineData(MeasurementUnit.Milliliters, "ml")]
    [InlineData(MeasurementUnit.Liters, "L")]
    [InlineData(MeasurementUnit.Cups, "cup(s)")]
    [InlineData(MeasurementUnit.Tablespoons, "tbsp")]
    [InlineData(MeasurementUnit.Teaspoons, "tsp")]
    [InlineData(MeasurementUnit.FluidOunces, "fl oz")]
    [InlineData(MeasurementUnit.Pieces, "piece(s)")]
    [InlineData((MeasurementUnit)999, "")]
    public void GetUnitDisplayName_MeasurementUnit_ReturnsAbbreviation(MeasurementUnit unit, string expected)
    {
        UnitConversionService.GetUnitDisplayName(unit).ShouldBe(expected);
    }

    [Theory]
    [InlineData("g", "g")]
    [InlineData("ml", "ml")]
    [InlineData("pieces", "piece(s)")]
    [InlineData("pinch", "pinch")]
    public void GetUnitDisplayName_BaseUnitString_ReturnsDisplayNameOrInput(string baseUnit, string expected)
    {
        UnitConversionService.GetUnitDisplayName(baseUnit).ShouldBe(expected);
    }

    [Fact]
    public void EveryMeasurementUnit_HasConversionAndDisplayName()
    {
        // Guards against adding a new enum member and forgetting to update the switches.
        foreach (var unit in Enum.GetValues<MeasurementUnit>())
        {
            UnitConversionService.GetUnitDisplayName(unit).ShouldNotBeNullOrEmpty($"{unit} has no display name");

            var (_, baseUnit) = UnitConversionService.ConvertToBaseUnit(1m, unit);
            new[] { "g", "ml", "pieces" }.ShouldContain(baseUnit);
        }
    }
}
