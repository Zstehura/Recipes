namespace RecipesApp.Models;

public class IngredientEntry
{
    public string Name { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public MeasurementUnit Unit { get; set; } = MeasurementUnit.Pieces;
}

