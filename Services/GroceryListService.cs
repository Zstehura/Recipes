using System.Text.Json.Serialization;
using RecipesApp.Models;

namespace RecipesApp.Services;

/// <summary>One line of a grocery list: "1.5 kg Flour", "3 cups Rice", or just "Salt".</summary>
/// <param name="Name">The ingredient, with its preparation note if it has one: "Tomatoes (diced)".</param>
/// <param name="Quantity">The total, or null when no recipe gave an amount ("salt to taste").</param>
/// <param name="Unit">The unit of <paramref name="Quantity"/>, or null with it.</param>
public sealed record GroceryItem(string Name, decimal? Quantity, MeasurementUnit? Unit)
{
    /// <summary>Identifies the item for ticking it off; the same name can appear in two units.</summary>
    [JsonIgnore]
    public string Key => $"{Name}|{Unit}";

    /// <summary>"1.5 kg", "trace amount", or empty when there's no amount.</summary>
    [JsonIgnore]
    public string Amount => Quantity switch
    {
        null => string.Empty,
        < 0.01m => "trace amount",   // too small to show at two decimal places
        decimal quantity => UnitConversionService.FormatAmount(quantity, Unit ?? MeasurementUnit.Pieces),
    };

    /// <summary>The item as one line of plain text, for copying or sharing.</summary>
    public string ToText() => Quantity switch
    {
        null => Name,
        < 0.01m => $"{Name} ({Amount})",
        _ => $"{Amount} {Name}",
    };
}

/// <summary>The combined list for the selected recipes, alphabetical by ingredient.</summary>
public sealed record CombinedGroceryList(int RecipeCount, List<GroceryItem> Items)
{
    /// <summary>Plain text for Copy and Share: a title, a blank line, then one "- " line per item.</summary>
    public string ToText()
    {
        var lines = new List<string> { "Grocery list", string.Empty };
        lines.AddRange(Items.Select(i => $"- {i.ToText()}"));
        return string.Join(Environment.NewLine, lines);
    }
}

public class GroceryListService
{
    private readonly IRecipeReader _recipeReader;

    public GroceryListService(IRecipeReader recipeReader)
    {
        _recipeReader = recipeReader;
    }

    /// <summary>
    /// Loads the selected recipes and combines their ingredients. Recipes that no longer exist
    /// are skipped, so the list can be empty.
    /// </summary>
    /// <param name="recipeMultipliers">Recipe ID to how many batches of it.</param>
    public async Task<CombinedGroceryList> GenerateGroceryListAsync(Dictionary<int, int>? recipeMultipliers)
    {
        var recipes = new List<Recipe>();
        foreach (var recipeId in recipeMultipliers?.Keys ?? Enumerable.Empty<int>())
        {
            var recipe = await _recipeReader.GetRecipeByIdAsync(recipeId);
            if (recipe != null)
            {
                recipes.Add(recipe);
            }
        }

        return BuildGroceryList(recipes, recipeMultipliers ?? new());
    }

    /// <summary>
    /// Adds up ingredients across the given recipes. Pure function: no database access, so it
    /// can be unit tested directly.
    /// </summary>
    /// <remarks>
    /// Lines are grouped by ingredient, preparation note, and kind of unit (weight, volume, or
    /// count), so "200 g flour" and "2 pieces flour" stay separate. A group whose lines were all
    /// entered in the same unit keeps that unit ("1 cup" + "2 cups" = "3 cups"). Otherwise the
    /// lines are converted to grams or milliliters, added, and shown in a readable unit
    /// ("1.2 kg" rather than "1200 g").
    /// </remarks>
    /// <param name="recipes">Recipes with their ingredients loaded.</param>
    /// <param name="recipeMultipliers">Recipe ID to quantity multiplier for every recipe given.</param>
    public static CombinedGroceryList BuildGroceryList(List<Recipe> recipes, Dictionary<int, int> recipeMultipliers)
    {
        var groups = new Dictionary<(string Name, string Modifier, string BaseUnit), Group>(GroupKeyComparer.Instance);

        foreach (var recipe in recipes)
        {
            var multiplier = recipeMultipliers[recipe.Id];

            foreach (var line in recipe.RecipeIngredients)
            {
                var unit = line.Unit ?? MeasurementUnit.Pieces;
                var (_, baseUnit) = UnitConversionService.ConvertToBaseUnit(0, unit);
                var key = (line.Ingredient.Name, line.Modifier ?? string.Empty, baseUnit);

                if (!groups.TryGetValue(key, out var group))
                {
                    group = new Group(line.Ingredient.Name, line.Modifier ?? string.Empty, baseUnit);
                    groups[key] = group;
                }

                if (line.Quantity is decimal quantity)
                {
                    group.Add(quantity * multiplier, unit);
                }
            }
        }

        var items = groups.Values
            .Select(g => g.ToItem())
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new CombinedGroceryList(recipes.Count, items);
    }

    private sealed class Group(string name, string modifier, string baseUnit)
    {
        private readonly HashSet<MeasurementUnit> _units = new();
        private decimal _inEnteredUnit;
        private decimal _inBaseUnit;

        public void Add(decimal quantity, MeasurementUnit unit)
        {
            _units.Add(unit);
            _inEnteredUnit += quantity;
            _inBaseUnit += UnitConversionService.ConvertToBaseUnit(quantity, unit).quantity;
        }

        public GroceryItem ToItem()
        {
            var displayName = IngredientService.NormalizeName(name);
            if (!string.IsNullOrWhiteSpace(modifier))
            {
                displayName = $"{displayName} ({modifier})";
            }

            // No line gave an amount (e.g. "salt to taste")
            if (_units.Count == 0)
                return new GroceryItem(displayName, null, null);

            // One unit throughout, and not grams or ml (those read better scaled up to kg / L)
            var only = _units.Count == 1 ? _units.First() : (MeasurementUnit?)null;
            if (only is MeasurementUnit unit and not (MeasurementUnit.Grams or MeasurementUnit.Milliliters))
                return new GroceryItem(displayName, _inEnteredUnit, unit);

            var (quantity, readableUnit) = UnitConversionService.ConvertFromBaseUnit(_inBaseUnit, baseUnit);
            return new GroceryItem(displayName, quantity, readableUnit);
        }
    }

    // Ingredient names compare without case ("Flour" and "flour" are one item), as the
    // ingredient table does; notes and units compare exactly as before
    private sealed class GroupKeyComparer : IEqualityComparer<(string Name, string Modifier, string BaseUnit)>
    {
        public static readonly GroupKeyComparer Instance = new();

        public bool Equals((string Name, string Modifier, string BaseUnit) x, (string Name, string Modifier, string BaseUnit) y) =>
            string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Modifier, y.Modifier, StringComparison.OrdinalIgnoreCase)
            && x.BaseUnit == y.BaseUnit;

        public int GetHashCode((string Name, string Modifier, string BaseUnit) key) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(key.Name),
            StringComparer.OrdinalIgnoreCase.GetHashCode(key.Modifier),
            key.BaseUnit);
    }
}
