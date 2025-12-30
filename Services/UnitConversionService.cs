using RecipesApp.Models;

namespace RecipesApp.Services;

public class UnitConversionService
{
    // Base units: grams for weight, milliliters for volume, pieces for count
    public const string BASE_WEIGHT_UNIT = "g";
    public const string BASE_VOLUME_UNIT = "ml";
    public const string BASE_COUNT_UNIT = "pieces";

    public static (decimal quantity, string unit) ConvertToBaseUnit(decimal quantity, MeasurementUnit fromUnit)
    {
        return fromUnit switch
        {
            // Weight conversions to grams
            MeasurementUnit.Grams => (quantity, BASE_WEIGHT_UNIT),
            MeasurementUnit.Kilograms => (quantity * 1000m, BASE_WEIGHT_UNIT),
            MeasurementUnit.Ounces => (quantity * 28.3495m, BASE_WEIGHT_UNIT),
            MeasurementUnit.Pounds => (quantity * 453.592m, BASE_WEIGHT_UNIT),

            // Volume conversions to milliliters
            MeasurementUnit.Milliliters => (quantity, BASE_VOLUME_UNIT),
            MeasurementUnit.Liters => (quantity * 1000m, BASE_VOLUME_UNIT),
            MeasurementUnit.Cups => (quantity * 236.588m, BASE_VOLUME_UNIT),
            MeasurementUnit.Tablespoons => (quantity * 14.7868m, BASE_VOLUME_UNIT),
            MeasurementUnit.Teaspoons => (quantity * 4.92892m, BASE_VOLUME_UNIT),
            MeasurementUnit.FluidOunces => (quantity * 29.5735m, BASE_VOLUME_UNIT),

            // Count units
            MeasurementUnit.Pieces => (quantity, BASE_COUNT_UNIT),

            _ => (quantity, BASE_COUNT_UNIT)
        };
    }

    public static (decimal quantity, MeasurementUnit unit) ConvertFromBaseUnit(decimal quantity, string baseUnit)
    {
        // Convert back to a reasonable display unit for better readability
        return baseUnit switch
        {
            BASE_WEIGHT_UNIT => quantity >= 1000 
                ? (quantity / 1000m, MeasurementUnit.Kilograms)
                : (quantity, MeasurementUnit.Grams),
            BASE_VOLUME_UNIT => quantity >= 1000
                ? (quantity / 1000m, MeasurementUnit.Liters)
                : (quantity, MeasurementUnit.Milliliters),
            BASE_COUNT_UNIT => (quantity, MeasurementUnit.Pieces),
            _ => (quantity, MeasurementUnit.Pieces)
        };
    }

    public static string GetUnitDisplayName(MeasurementUnit unit)
    {
        return unit switch
        {
            MeasurementUnit.Grams => "g",
            MeasurementUnit.Kilograms => "kg",
            MeasurementUnit.Ounces => "oz",
            MeasurementUnit.Pounds => "lb",
            MeasurementUnit.Milliliters => "ml",
            MeasurementUnit.Liters => "L",
            MeasurementUnit.Cups => "cup(s)",
            MeasurementUnit.Tablespoons => "tbsp",
            MeasurementUnit.Teaspoons => "tsp",
            MeasurementUnit.FluidOunces => "fl oz",
            MeasurementUnit.Pieces => "piece(s)",
            _ => ""
        };
    }

    public static string GetUnitDisplayName(string baseUnit)
    {
        return baseUnit switch
        {
            BASE_WEIGHT_UNIT => "g",
            BASE_VOLUME_UNIT => "ml",
            BASE_COUNT_UNIT => "piece(s)",
            _ => baseUnit
        };
    }
}

