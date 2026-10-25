using RecipesApp.Models;

namespace Recipes.Tests.TestData;

/// <summary>
/// Lets tests write units briefly: "g", "ml", and "pieces" (how older versions stored them),
/// or any <see cref="MeasurementUnit"/> name such as "Cups".
/// </summary>
public static class TestUnits
{
    public static MeasurementUnit? Parse(string? unit) => unit switch
    {
        null => null,
        "g" => MeasurementUnit.Grams,
        "ml" => MeasurementUnit.Milliliters,
        "pieces" => MeasurementUnit.Pieces,
        _ => Enum.Parse<MeasurementUnit>(unit, ignoreCase: true),
    };
}
