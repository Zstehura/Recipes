using System.ComponentModel.DataAnnotations;

namespace RecipesApp.Models;

public class IngredientEntry
{
    [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "1000000", ErrorMessage = "Quantity must be between 0 and 1,000,000")]
    public decimal? Quantity { get; set; }

    [EnumDataType(typeof(MeasurementUnit), ErrorMessage = "Unknown unit")]
    public MeasurementUnit Unit { get; set; } = MeasurementUnit.Pieces;

    [StringLength(100, ErrorMessage = "Preparation note cannot exceed 100 characters")]
    public string Modifier { get; set; } = string.Empty;
}
