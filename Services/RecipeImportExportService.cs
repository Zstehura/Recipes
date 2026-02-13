using System.Text;
using System.Text.RegularExpressions;
using RecipesApp.Models;

namespace RecipesApp.Services;

public class RecipeImportExportService
{
    private const string RecipeDelimiter = "===== RECIPE =====";
    private const string IngredientsDelimiter = "===== INGREDIENTS =====";
    private const string InstructionsDelimiter = "===== INSTRUCTIONS =====";
    private const string NotesDelimiter = "===== NOTES =====";
    private const string EndRecipeDelimiter = "===== END RECIPE =====";

    /// <summary>
    /// Exports a single recipe to the custom text format
    /// </summary>
    public string ExportRecipeToText(Recipe recipe)
    {
        var sb = new StringBuilder();

        sb.AppendLine(RecipeDelimiter);
        sb.AppendLine($"Name: {recipe.Name}");
        sb.AppendLine($"Cooking Time: {recipe.CookingTime} minutes");
        sb.AppendLine($"Servings: {recipe.Servings}");

        if (!string.IsNullOrWhiteSpace(recipe.Tags))
        {
            sb.AppendLine($"Tags: {recipe.Tags}");
        }

        sb.AppendLine($"Created: {FormatDate(recipe.CreatedDate)}");
        sb.AppendLine($"Modified: {FormatDate(recipe.ModifiedDate)}");
        sb.AppendLine();

        sb.AppendLine(IngredientsDelimiter);
        foreach (var ri in recipe.RecipeIngredients.OrderBy(ri => ri.Ingredient.Name))
        {
            var quantity = ri.Quantity ?? 0;
            var unit = ri.Unit ?? "pieces";
            sb.AppendLine($"{quantity}{unit} {ri.Ingredient.Name}");
        }
        sb.AppendLine();

        sb.AppendLine(InstructionsDelimiter);
        sb.AppendLine(recipe.Instructions);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(recipe.Notes))
        {
            sb.AppendLine(NotesDelimiter);
            sb.AppendLine(recipe.Notes);
            sb.AppendLine();
        }

        sb.AppendLine(EndRecipeDelimiter);

        return sb.ToString();
    }

    /// <summary>
    /// Exports multiple recipes to a single text file
    /// </summary>
    public string ExportRecipesToText(List<Recipe> recipes)
    {
        var sb = new StringBuilder();

        foreach (var recipe in recipes.OrderBy(r => r.Name))
        {
            sb.Append(ExportRecipeToText(recipe));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parses a text file containing one or more recipes.
    /// Returns a list of successfully parsed recipes and a list of error messages.
    /// </summary>
    /// <param name="text">The recipe text content to parse</param>
    /// <returns>A tuple containing the list of parsed recipes and any error messages</returns>
    /// <remarks>
    /// <para><b>Format Structure:</b></para>
    /// <code>
    /// ===== RECIPE =====
    /// Name: Recipe Name (Required)
    /// Cooking Time: 30 minutes (Required, can also be "30 min" or "30")
    /// Servings: 4 (Required)
    /// Tags: Italian,Vegetarian (Optional, comma-separated)
    /// Created: 2024-01-15 (Optional, will use current date if not provided)
    /// Modified: 2024-01-20 (Optional, will use current date if not provided)
    ///
    /// ===== INGREDIENTS =====
    /// 250g flour
    /// 2cups milk
    /// 3 eggs
    /// 1/2 tsp salt
    /// 1 1/4 cups sugar
    ///
    /// ===== INSTRUCTIONS =====
    /// Mix the flour and milk together.
    /// Add the eggs one at a time.
    /// Bake at 350F for 30 minutes.
    ///
    /// ===== NOTES =====
    /// Optional notes about the recipe.
    ///
    /// ===== END RECIPE =====
    /// </code>
    ///
    /// <para><b>Required Fields:</b></para>
    /// <list type="bullet">
    /// <item>Name - Recipe name (max 200 characters)</item>
    /// <item>Cooking Time - Must be at least 1 minute</item>
    /// <item>Servings - Must be at least 1</item>
    /// <item>Instructions - Cooking instructions</item>
    /// </list>
    ///
    /// <para><b>Optional Fields:</b></para>
    /// <list type="bullet">
    /// <item>Tags - Comma-separated list of tags</item>
    /// <item>Notes - Additional notes</item>
    /// <item>Created/Modified dates - Will default to current date if not provided</item>
    /// </list>
    ///
    /// <para><b>Ingredient Format:</b></para>
    /// <para>Format: <c>quantity unit name</c> or <c>quantity name</c> (e.g., "250g flour", "2cups water", "2 eggs")</para>
    /// <para>Spaces between quantity and unit are optional.</para>
    /// <para>If no unit is specified, the ingredient defaults to "pieces" (e.g., "2 eggs" becomes "2 pieces eggs").</para>
    /// <para>Fractions are supported: "3/4 cup sugar" or "1 1/2 tsp vanilla" will be converted to decimals (0.75, 1.5).</para>
    /// <para>Ingredient names are case-insensitive; duplicate ingredients will be merged automatically.</para>
    /// <para>If an ingredient line doesn't match any format, it will be treated as just an ingredient name.</para>
    ///
    /// <para><b>Supported Units:</b></para>
    /// <list type="bullet">
    /// <item><b>Weight:</b> g/gram/grams, kg/kilogram/kilograms, oz/ounce/ounces, lb/lbs/pound/pounds</item>
    /// <item><b>Volume:</b> ml/milliliter/milliliters, l/liter/liters, cup/cups, tbsp/tablespoon/tablespoons, tsp/teaspoon/teaspoons, fl oz/floz/fluidounce/fluidounces</item>
    /// <item><b>Count:</b> piece/pieces/pcs/pc</item>
    /// </list>
    ///
    /// <para><b>Multiple Recipes:</b></para>
    /// <para>You can include multiple recipes in a single file. Each recipe must end with <c>===== END RECIPE =====</c></para>
    ///
    /// <para><b>Error Handling:</b></para>
    /// <para>If a recipe fails to parse, an error will be added to the errors list, but other valid recipes will still be parsed.</para>
    /// </remarks>
    public (List<Recipe> recipes, List<string> errors) ParseRecipeText(string text)
    {
        var recipes = new List<Recipe>();
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add("File is empty");
            return (recipes, errors);
        }

        // Split by END RECIPE delimiter to handle multiple recipes
        var recipeBlocks = text.Split(new[] { EndRecipeDelimiter }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < recipeBlocks.Length; i++)
        {
            var block = recipeBlocks[i].Trim();
            if (string.IsNullOrWhiteSpace(block))
            {
                continue;
            }

            try
            {
                var recipe = ParseSingleRecipe(block, i + 1);
                recipes.Add(recipe);
            }
            catch (Exception ex)
            {
                errors.Add($"Recipe #{i + 1}: {ex.Message}");
            }
        }

        if (recipes.Count == 0 && errors.Count == 0)
        {
            errors.Add("No valid recipe blocks found. Make sure recipes are properly formatted with delimiters.");
        }

        return (recipes, errors);
    }

    private Recipe ParseSingleRecipe(string block, int recipeNumber)
    {
        var recipe = new Recipe
        {
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now
        };

        // Remove the RECIPE delimiter if present
        block = block.Replace(RecipeDelimiter, "").Trim();

        // Split into sections
        var sections = new Dictionary<string, string>();
        var currentSection = "METADATA"; // Default section before any delimiter
        var sectionContent = new StringBuilder();

        var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine == IngredientsDelimiter)
            {
                if (!string.IsNullOrEmpty(currentSection))
                {
                    sections[currentSection] = sectionContent.ToString().Trim();
                }
                currentSection = "INGREDIENTS";
                sectionContent.Clear();
            }
            else if (trimmedLine == InstructionsDelimiter)
            {
                if (!string.IsNullOrEmpty(currentSection))
                {
                    sections[currentSection] = sectionContent.ToString().Trim();
                }
                currentSection = "INSTRUCTIONS";
                sectionContent.Clear();
            }
            else if (trimmedLine == NotesDelimiter)
            {
                if (!string.IsNullOrEmpty(currentSection))
                {
                    sections[currentSection] = sectionContent.ToString().Trim();
                }
                currentSection = "NOTES";
                sectionContent.Clear();
            }
            else if (string.IsNullOrEmpty(currentSection))
            {
                // We're in the metadata section (before any delimiter)
                sectionContent.AppendLine(line);
            }
            else
            {
                // We're in a named section
                sectionContent.AppendLine(line);
            }
        }

        // Add the last section
        if (!string.IsNullOrEmpty(currentSection))
        {
            sections[currentSection] = sectionContent.ToString().Trim();
        }
        else if (sectionContent.Length > 0)
        {
            // Metadata section
            sections["METADATA"] = sectionContent.ToString().Trim();
        }

        // Parse metadata
        if (sections.ContainsKey("METADATA"))
        {
            ParseMetadata(sections["METADATA"], recipe);
        }

        // Parse ingredients
        if (sections.ContainsKey("INGREDIENTS"))
        {
            ParseIngredients(sections["INGREDIENTS"], recipe);
        }

        // Parse instructions
        if (sections.ContainsKey("INSTRUCTIONS"))
        {
            recipe.Instructions = sections["INSTRUCTIONS"];
        }

        // Parse notes
        if (sections.ContainsKey("NOTES"))
        {
            recipe.Notes = sections["NOTES"];
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(recipe.Name))
        {
            throw new Exception("Missing required field: Name");
        }

        if (recipe.CookingTime < 1)
        {
            throw new Exception("Missing or invalid required field: Cooking Time (must be at least 1 minute)");
        }

        if (recipe.Servings < 1)
        {
            throw new Exception("Missing or invalid required field: Servings (must be at least 1)");
        }

        if (string.IsNullOrWhiteSpace(recipe.Instructions))
        {
            throw new Exception("Missing required field: Instructions");
        }

        return recipe;
    }

    private void ParseMetadata(string metadata, Recipe recipe)
    {
        var lines = metadata.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex < 0)
            {
                continue;
            }

            var key = line.Substring(0, colonIndex).Trim();
            var value = line.Substring(colonIndex + 1).Trim();

            switch (key.ToLower())
            {
                case "name":
                    recipe.Name = value;
                    break;

                case "cooking time":
                    recipe.CookingTime = ParseCookingTime(value);
                    break;

                case "servings":
                    if (int.TryParse(value, out var servings))
                    {
                        recipe.Servings = servings;
                    }
                    break;

                case "tags":
                    recipe.Tags = value;
                    break;

                case "created":
                    var createdDate = TryParseDate(value);
                    if (createdDate.HasValue)
                    {
                        recipe.CreatedDate = createdDate.Value;
                    }
                    break;

                case "modified":
                    var modifiedDate = TryParseDate(value);
                    if (modifiedDate.HasValue)
                    {
                        recipe.ModifiedDate = modifiedDate.Value;
                    }
                    break;
            }
        }
    }

    private int ParseCookingTime(string value)
    {
        // Handle formats like "25 minutes", "25 min", "25"
        var match = Regex.Match(value, @"(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var minutes))
        {
            return minutes;
        }
        return 0;
    }

    private DateTime? TryParseDate(string dateStr)
    {
        if (DateTime.TryParse(dateStr, out var date))
        {
            return date;
        }
        return null;
    }

    private void ParseIngredients(string ingredientsText, Recipe recipe)
    {
        var lines = ingredientsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                continue;
            }

            var entry = ParseIngredientLine(trimmedLine);
            if (entry != null)
            {
                // Create a temporary RecipeIngredient with just the name
                // The actual ingredient lookup and unit conversion will be handled by RecipeService
                var recipeIngredient = new RecipeIngredient
                {
                    Ingredient = new Ingredient { Name = entry.Name },
                    Quantity = entry.Quantity,
                    Unit = ConvertUnitToBaseUnit(entry.Unit)
                };
                recipe.RecipeIngredients.Add(recipeIngredient);
            }
        }
    }

    private IngredientEntry? ParseIngredientLine(string line)
    {
        // Try to parse format with unit: "250g flour" or "2 pieces eggs"
        // Regex: (quantity with optional fraction) (unit) (ingredient name)
        // Updated to allow hyphens in units (e.g., "fl-oz")
        var matchWithUnit = Regex.Match(line, @"^([\d\s\/\.]+)\s*([a-zA-Z\-]+)\s+(.+)$");

        if (matchWithUnit.Success)
        {
            var quantityStr = matchWithUnit.Groups[1].Value.Trim();
            var unitStr = matchWithUnit.Groups[2].Value;
            var name = matchWithUnit.Groups[3].Value.Trim();

            var quantity = ParseQuantity(quantityStr);
            var unit = TryParseUnit(unitStr);
            if (quantity.HasValue && unit.HasValue)
            {
                return new IngredientEntry
                {
                    Name = name,
                    Quantity = quantity,
                    Unit = unit.Value
                };
            }
        }

        // Try to parse format without unit: "2 eggs" (quantity + name)
        var matchWithoutUnit = Regex.Match(line, @"^([\d\s\/\.]+)\s+(.+)$");
        if (matchWithoutUnit.Success)
        {
            var quantityStr = matchWithoutUnit.Groups[1].Value.Trim();
            var name = matchWithoutUnit.Groups[2].Value.Trim();

            var quantity = ParseQuantity(quantityStr);
            if (quantity.HasValue)
            {
                // Default to pieces when no unit is specified
                return new IngredientEntry
                {
                    Name = name,
                    Quantity = quantity,
                    Unit = MeasurementUnit.Pieces
                };
            }
        }

        // Fallback: treat entire line as ingredient name with no quantity
        return new IngredientEntry
        {
            Name = line,
            Quantity = null,
            Unit = MeasurementUnit.Pieces
        };
    }

    private decimal? ParseQuantity(string quantityStr)
    {
        // Handle fractions like "3/4", "1/2"
        var fractionMatch = Regex.Match(quantityStr, @"^(\d+)\s*\/\s*(\d+)$");
        if (fractionMatch.Success)
        {
            if (decimal.TryParse(fractionMatch.Groups[1].Value, out var numerator) &&
                decimal.TryParse(fractionMatch.Groups[2].Value, out var denominator) &&
                denominator != 0)
            {
                return numerator / denominator;
            }
        }

        // Handle mixed fractions like "1 1/2", "2 3/4"
        var mixedMatch = Regex.Match(quantityStr, @"^(\d+)\s+(\d+)\s*\/\s*(\d+)$");
        if (mixedMatch.Success)
        {
            if (decimal.TryParse(mixedMatch.Groups[1].Value, out var whole) &&
                decimal.TryParse(mixedMatch.Groups[2].Value, out var numerator) &&
                decimal.TryParse(mixedMatch.Groups[3].Value, out var denominator) &&
                denominator != 0)
            {
                return whole + (numerator / denominator);
            }
        }

        // Handle regular decimal numbers
        if (decimal.TryParse(quantityStr, out var quantity))
        {
            return quantity;
        }

        return null;
    }

    private MeasurementUnit? TryParseUnit(string unitStr)
    {
        var unit = unitStr.ToLower().Trim();

        return unit switch
        {
            "g" or "gram" or "grams" => MeasurementUnit.Grams,
            "kg" or "kilogram" or "kilograms" => MeasurementUnit.Kilograms,
            "oz" or "ounce" or "ounces" => MeasurementUnit.Ounces,
            "lb" or "lbs" or "pound" or "pounds" => MeasurementUnit.Pounds,
            "ml" or "milliliter" or "milliliters" => MeasurementUnit.Milliliters,
            "l" or "liter" or "liters" => MeasurementUnit.Liters,
            "cup" or "cups" => MeasurementUnit.Cups,
            "tbsp" or "tablespoon" or "tablespoons" => MeasurementUnit.Tablespoons,
            "tsp" or "teaspoon" or "teaspoons" => MeasurementUnit.Teaspoons,
            "fl oz" or "floz" or "fluidounce" or "fluidounces" => MeasurementUnit.FluidOunces,
            "piece" or "pieces" or "pcs" or "pc" => MeasurementUnit.Pieces,
            _ => null
        };
    }

    private string? ConvertUnitToBaseUnit(MeasurementUnit unit)
    {
        // Convert display units to base units (g, ml, pieces)
        // This matches the database storage format
        return unit switch
        {
            MeasurementUnit.Grams or MeasurementUnit.Kilograms or MeasurementUnit.Ounces or MeasurementUnit.Pounds => "g",
            MeasurementUnit.Milliliters or MeasurementUnit.Liters or MeasurementUnit.Cups or MeasurementUnit.Tablespoons or MeasurementUnit.Teaspoons or MeasurementUnit.FluidOunces => "ml",
            MeasurementUnit.Pieces => "pieces",
            _ => "pieces"
        };
    }

    private string FormatDate(DateTime date)
    {
        return date.ToString("yyyy-MM-dd");
    }
}
