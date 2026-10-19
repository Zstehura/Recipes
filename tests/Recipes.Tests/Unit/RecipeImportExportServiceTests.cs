using System.Globalization;
using Recipes.Tests.TestData;
using RecipesApp.Models;
using RecipesApp.Services;
using Shouldly;

namespace Recipes.Tests.Unit;

public class RecipeImportExportServiceTests
{
    private readonly RecipeImportExportService _service = new();

    private RecipeIngredient ParseSingleIngredient(string line)
    {
        var (recipes, errors) = _service.ParseRecipeText(RecipeTextBuilder.Build(ingredients: new[] { line }));
        errors.ShouldBeEmpty();
        return recipes.ShouldHaveSingleItem().RecipeIngredients.ShouldHaveSingleItem();
    }

    // ---------------------------------------------------------------- Parsing: valid input

    [Fact]
    public void ParseRecipeText_CompleteRecipe_ParsesAllFields()
    {
        var text = RecipeTextBuilder.Build(
            name: "Pancakes",
            cookingTime: "25 minutes",
            servings: "4",
            tags: "Breakfast,Sweet",
            created: "2024-01-15",
            ingredients: new[] { "250g flour", "3 eggs" },
            instructions: "Mix the flour and eggs.\nCook on a hot pan.",
            notes: "Best served warm.");

        var (recipes, errors) = _service.ParseRecipeText(text);

        errors.ShouldBeEmpty();
        var recipe = recipes.ShouldHaveSingleItem();
        recipe.Name.ShouldBe("Pancakes");
        recipe.CookingTime.ShouldBe(25);
        recipe.Servings.ShouldBe(4);
        recipe.Tags.ShouldBe("Breakfast,Sweet");
        recipe.CreatedDate.ShouldBe(new DateTime(2024, 1, 15));
        recipe.Instructions.ShouldBe("Mix the flour and eggs.\nCook on a hot pan.");
        recipe.Notes.ShouldBe("Best served warm.");
        recipe.RecipeIngredients.Count.ShouldBe(2);
    }

    [Fact]
    public void ParseRecipeText_OptionalFieldsMissing_UsesDefaults()
    {
        var before = DateTime.Now;

        var (recipes, errors) = _service.ParseRecipeText(RecipeTextBuilder.Build());

        errors.ShouldBeEmpty();
        var recipe = recipes.ShouldHaveSingleItem();
        recipe.Tags.ShouldBe(string.Empty);
        recipe.Notes.ShouldBe(string.Empty);
        recipe.CreatedDate.ShouldBeGreaterThanOrEqualTo(before);
        recipe.ModifiedDate.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Theory]
    [InlineData("30 minutes", 30)]
    [InlineData("30 min", 30)]
    [InlineData("30", 30)]
    [InlineData("about 45 mins", 45)]
    public void ParseRecipeText_CookingTimeFormats_ExtractsMinutes(string cookingTime, int expected)
    {
        var (recipes, _) = _service.ParseRecipeText(RecipeTextBuilder.Build(cookingTime: cookingTime));

        recipes.ShouldHaveSingleItem().CookingTime.ShouldBe(expected);
    }

    [Fact]
    public void ParseRecipeText_CookingTimeInHours_TakesFirstNumberOnly()
    {
        // Documents current behavior: the format is minutes only, so "1 hour 30 minutes" becomes 1.
        var (recipes, _) = _service.ParseRecipeText(RecipeTextBuilder.Build(cookingTime: "1 hour 30 minutes"));

        recipes.ShouldHaveSingleItem().CookingTime.ShouldBe(1);
    }

    [Fact]
    public void ParseRecipeText_NameContainsColon_KeepsEverythingAfterFirstColon()
    {
        var (recipes, _) = _service.ParseRecipeText(RecipeTextBuilder.Build(name: "Soup: Tomato"));

        recipes.ShouldHaveSingleItem().Name.ShouldBe("Soup: Tomato");
    }

    [Fact]
    public void ParseRecipeText_MetadataKeysAreCaseInsensitive()
    {
        var text = RecipeTextBuilder.Build()
            .Replace("Name:", "NAME:")
            .Replace("Cooking Time:", "cooking time:");

        var (recipes, errors) = _service.ParseRecipeText(text);

        errors.ShouldBeEmpty();
        recipes.ShouldHaveSingleItem().Name.ShouldBe("Pancakes");
    }

    // ---------------------------------------------------------------- Parsing: ingredient lines

    [Theory]
    [InlineData("250g flour", 250, "g", "flour")]
    [InlineData("250 g flour", 250, "g", "flour")]
    [InlineData("250 grams flour", 250, "g", "flour")]
    [InlineData("100ml milk", 100, "ml", "milk")]
    [InlineData("1.5 l water", 1500, "ml", "water")]
    [InlineData("2 pieces eggs", 2, "pieces", "eggs")]
    [InlineData("3 eggs", 3, "pieces", "eggs")]
    [InlineData("3 large eggs", 3, "pieces", "large eggs")]
    [InlineData("2 cloves garlic", 2, "pieces", "cloves garlic")]
    [InlineData("1 TBSP oil", 14.7868, "ml", "oil")]
    public void ParseRecipeText_IngredientLine_ParsesQuantityUnitAndName(
        string line, double expectedQuantity, string expectedBaseUnit, string expectedName)
    {
        var ingredient = ParseSingleIngredient(line);

        ingredient.Quantity.ShouldBe((decimal)expectedQuantity);
        ingredient.Unit.ShouldBe(expectedBaseUnit);
        ingredient.Ingredient.Name.ShouldBe(expectedName);
    }

    [Theory]
    [InlineData("1/2 tsp salt", 2.46446)]
    [InlineData("3/4 cup sugar", 177.441)]
    [InlineData("1 1/4 cups sugar", 295.735)]
    [InlineData("2 1/2 eggs", 2.5)]
    public void ParseRecipeText_FractionQuantity_ConvertsToDecimalInBaseUnit(string line, double expected)
    {
        ParseSingleIngredient(line).Quantity.ShouldBe((decimal)expected);
    }

    [Theory]
    [InlineData("2 tomatoes (diced)", "tomatoes", "diced")]
    [InlineData("100g cheese (finely grated)", "cheese", "finely grated")]
    [InlineData("salt (to taste)", "salt", "to taste")]
    public void ParseRecipeText_IngredientWithParentheses_ExtractsModifier(
        string line, string expectedName, string expectedModifier)
    {
        var ingredient = ParseSingleIngredient(line);

        ingredient.Ingredient.Name.ShouldBe(expectedName);
        ingredient.Modifier.ShouldBe(expectedModifier);
    }

    [Fact]
    public void ParseRecipeText_IngredientWithoutModifier_HasNullModifier()
    {
        ParseSingleIngredient("250g flour").Modifier.ShouldBeNull();
    }

    [Fact]
    public void ParseRecipeText_IngredientWithoutQuantity_HasNullQuantity()
    {
        var ingredient = ParseSingleIngredient("salt");

        ingredient.Ingredient.Name.ShouldBe("salt");
        ingredient.Quantity.ShouldBeNull();
    }

    [Fact]
    public void ParseRecipeText_ZeroDenominator_TreatsWholeLineAsName()
    {
        var ingredient = ParseSingleIngredient("1/0 cup flour");

        ingredient.Quantity.ShouldBeNull();
        ingredient.Ingredient.Name.ShouldBe("1/0 cup flour");
    }

    [Fact]
    public void ParseRecipeText_SameIngredientOnTwoLines_KeepsBothEntries()
    {
        // Each line is its own entry; a recipe may list the same ingredient more than once.
        var (recipes, _) = _service.ParseRecipeText(
            RecipeTextBuilder.Build(ingredients: new[] { "100g flour", "50g flour" }));

        recipes.ShouldHaveSingleItem().RecipeIngredients.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("2 cups milk", 473.176, "ml")]
    [InlineData("1 kg flour", 1000, "g")]
    [InlineData("1 tbsp oil", 14.7868, "ml")]
    public void ParseRecipeText_NonBaseUnit_ConvertsQuantityToBaseUnit(
        string line, double expectedQuantity, string expectedBaseUnit)
    {
        var ingredient = ParseSingleIngredient(line);

        ingredient.Quantity.ShouldBe((decimal)expectedQuantity);
        ingredient.Unit.ShouldBe(expectedBaseUnit);
    }

    [Theory]
    [InlineData("2 fl oz cream")]
    [InlineData("2 FL OZ cream")]
    [InlineData("2 fl  oz cream")]
    [InlineData("2 fl-oz cream")]
    [InlineData("2floz cream")]
    public void ParseRecipeText_FluidOunceSpellings_ParseAsFluidOunces(string line)
    {
        var ingredient = ParseSingleIngredient(line);

        ingredient.Quantity.ShouldBe(59.147m);
        ingredient.Unit.ShouldBe("ml");
        ingredient.Ingredient.Name.ShouldBe("cream");
    }

    [Fact]
    public void ParseRecipeText_NameStartingWithFl_IsNotMistakenForFluidOunces()
    {
        var ingredient = ParseSingleIngredient("2 flowers");

        ingredient.Unit.ShouldBe("pieces");
        ingredient.Ingredient.Name.ShouldBe("flowers");
    }

    // ---------------------------------------------------------------- Parsing: whitespace

    [Fact]
    public void ParseRecipeText_ExtraWhitespaceAndWindowsLineEndings_ParsesCleanly()
    {
        var text = "\r\n\r\n   ===== RECIPE =====   \r\n" +
                   "   Name:    Pancakes   \r\n" +
                   "Cooking Time:   20   \r\n" +
                   "Servings:  4 \r\n" +
                   "\r\n" +
                   "  ===== INGREDIENTS =====  \r\n" +
                   "   250g   flour   \r\n" +
                   "\r\n\r\n" +
                   "===== INSTRUCTIONS =====\r\n" +
                   "   Mix.   \r\n" +
                   "===== END RECIPE =====\r\n\r\n";

        var (recipes, errors) = _service.ParseRecipeText(text);

        errors.ShouldBeEmpty();
        var recipe = recipes.ShouldHaveSingleItem();
        recipe.Name.ShouldBe("Pancakes");
        recipe.CookingTime.ShouldBe(20);
        recipe.Servings.ShouldBe(4);
        recipe.Instructions.ShouldBe("Mix.");
        var ingredient = recipe.RecipeIngredients.ShouldHaveSingleItem();
        ingredient.Ingredient.Name.ShouldBe("flour");
        ingredient.Quantity.ShouldBe(250m);
    }

    [Fact]
    public void ParseRecipeText_MissingEndDelimiter_StillParsesRecipe()
    {
        var (recipes, errors) = _service.ParseRecipeText(RecipeTextBuilder.Build(includeEndDelimiter: false));

        errors.ShouldBeEmpty();
        recipes.ShouldHaveSingleItem();
    }

    // ---------------------------------------------------------------- Parsing: malformed input

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    public void ParseRecipeText_EmptyInput_ReturnsFileIsEmptyError(string? text)
    {
        var (recipes, errors) = _service.ParseRecipeText(text!);

        recipes.ShouldBeEmpty();
        errors.ShouldHaveSingleItem().ShouldBe("File is empty");
    }

    [Fact]
    public void ParseRecipeText_OnlyDelimiters_ReturnsNoValidBlocksError()
    {
        var (recipes, errors) = _service.ParseRecipeText("===== END RECIPE =====\n===== END RECIPE =====");

        recipes.ShouldBeEmpty();
        errors.ShouldHaveSingleItem().ShouldStartWith("No valid recipe blocks found");
    }

    [Fact]
    public void ParseRecipeText_UnstructuredText_ReturnsMissingNameError()
    {
        var (recipes, errors) = _service.ParseRecipeText("just some random text");

        recipes.ShouldBeEmpty();
        errors.ShouldHaveSingleItem().ShouldBe("Recipe #1: Missing required field: Name");
    }

    public static TheoryData<string, string> MissingRequiredFieldCases => new()
    {
        { RecipeTextBuilder.Build(name: null), "Missing required field: Name" },
        { RecipeTextBuilder.Build(name: "   "), "Missing required field: Name" },
        { RecipeTextBuilder.Build(cookingTime: null), "Cooking Time" },
        { RecipeTextBuilder.Build(cookingTime: "soon"), "Cooking Time" },
        { RecipeTextBuilder.Build(cookingTime: "0"), "Cooking Time" },
        { RecipeTextBuilder.Build(servings: null), "Servings" },
        { RecipeTextBuilder.Build(servings: "four"), "Servings" },
        { RecipeTextBuilder.Build(servings: "-2"), "Servings" },
        { RecipeTextBuilder.Build(instructions: null), "Missing required field: Instructions" },
    };

    [Theory]
    [MemberData(nameof(MissingRequiredFieldCases))]
    public void ParseRecipeText_MissingOrInvalidRequiredField_ReturnsError(string text, string expectedError)
    {
        var (recipes, errors) = _service.ParseRecipeText(text);

        recipes.ShouldBeEmpty();
        errors.ShouldHaveSingleItem().ShouldContain(expectedError);
    }

    [Fact]
    public void ParseRecipeText_OneInvalidRecipeAmongValid_ParsesValidOnesAndReportsError()
    {
        var text = RecipeTextBuilder.Build(name: "First") + "\n" +
                   RecipeTextBuilder.Build(name: null) + "\n" +
                   RecipeTextBuilder.Build(name: "Third");

        var (recipes, errors) = _service.ParseRecipeText(text);

        recipes.Select(r => r.Name).ShouldBe(new[] { "First", "Third" });
        errors.ShouldHaveSingleItem().ShouldStartWith("Recipe #2:");
    }

    [Fact]
    public void ParseRecipeText_InvalidDate_FallsBackToNow()
    {
        var before = DateTime.Now;

        var (recipes, _) = _service.ParseRecipeText(RecipeTextBuilder.Build(created: "not a date"));

        recipes.ShouldHaveSingleItem().CreatedDate.ShouldBeGreaterThanOrEqualTo(before);
    }

    // ---------------------------------------------------------------- Parsing: limits and hostile input

    [Fact]
    public void ParseRecipeText_LargerThanImportLimit_ReturnsTooLargeError()
    {
        var text = RecipeTextBuilder.Build(notes: new string('a', RecipeImportExportService.MaxImportCharacters));

        var (recipes, errors) = _service.ParseRecipeText(text);

        recipes.ShouldBeEmpty();
        errors.ShouldHaveSingleItem().ShouldStartWith("The import is too large");
    }

    [Fact]
    public void ParseRecipeText_LargeButWithinLimit_Parses()
    {
        var text = RecipeTextBuilder.Build(notes: new string('a', RecipeImportExportService.MaxImportCharacters - 1000));

        var (recipes, errors) = _service.ParseRecipeText(text);

        errors.ShouldBeEmpty();
        recipes.ShouldHaveSingleItem();
    }

    [Fact]
    public void ParseRecipeText_IngredientLineAtLimit_Parses()
    {
        var line = "2 " + new string('x', RecipeImportExportService.MaxIngredientLineLength - 2);

        var (recipes, errors) = _service.ParseRecipeText(RecipeTextBuilder.Build(ingredients: new[] { line }));

        errors.ShouldBeEmpty();
        recipes.ShouldHaveSingleItem();
    }

    [Fact]
    public void ParseRecipeText_IngredientLineOverLimit_RejectsOnlyThatRecipe()
    {
        var tooLong = "2 " + new string('x', RecipeImportExportService.MaxIngredientLineLength);
        var text = RecipeTextBuilder.Build(name: "Bad", ingredients: new[] { tooLong }) + "\n" +
                   RecipeTextBuilder.Build(name: "Good");

        var (recipes, errors) = _service.ParseRecipeText(text);

        recipes.ShouldHaveSingleItem().Name.ShouldBe("Good");
        errors.ShouldHaveSingleItem().ShouldContain("ingredient line is too long");
    }

    [Fact]
    public void ParseRecipeText_ManyUnclosedParentheses_IsRejectedQuickly()
    {
        // Regression test: before the line limit, ~500 KB of " (" took about 4 seconds to parse,
        // growing with the square of the length (a regex denial-of-service).
        var hostileLine = "x" + string.Concat(Enumerable.Repeat(" (", 250_000));
        var text = RecipeTextBuilder.Build(ingredients: new[] { hostileLine });
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var (recipes, errors) = _service.ParseRecipeText(text);

        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000);
        recipes.ShouldBeEmpty();
        errors.ShouldHaveSingleItem().ShouldContain("ingredient line is too long");
    }

    [Fact]
    public void ParseRecipeText_QuantityTooLargeToConvert_ReturnsError()
    {
        var text = RecipeTextBuilder.Build(ingredients: new[] { "79228162514264337593543950335 kg flour" });

        var (recipes, errors) = _service.ParseRecipeText(text);

        recipes.ShouldBeEmpty();
        errors.ShouldHaveSingleItem().ShouldBe("Recipe #1: a quantity is too large.");
    }

    // ---------------------------------------------------------------- Export

    private static Recipe CreateStoredRecipe() => new()
    {
        Name = "Pancakes",
        CookingTime = 20,
        Servings = 4,
        Tags = "Breakfast,Sweet",
        Instructions = "Mix.\nCook.",
        Notes = "Serve warm.",
        CreatedDate = new DateTime(2024, 1, 15),
        ModifiedDate = new DateTime(2024, 2, 1),
        RecipeIngredients =
        {
            // Stored the way the database stores them: base units only
            new RecipeIngredient { Ingredient = new Ingredient { Name = "Milk" }, Quantity = 473.176m, Unit = "ml" },
            new RecipeIngredient { Ingredient = new Ingredient { Name = "Flour" }, Quantity = 250m, Unit = "g" },
            new RecipeIngredient { Ingredient = new Ingredient { Name = "Eggs" }, Quantity = 2m, Unit = "pieces", Modifier = "beaten" },
        },
    };

    [Fact]
    public void ExportRecipeToText_FullRecipe_WritesAllSectionsInOrder()
    {
        var text = _service.ExportRecipeToText(CreateStoredRecipe());

        var lines = text.Split(Environment.NewLine);
        lines.ShouldContain("Name: Pancakes");
        lines.ShouldContain("Cooking Time: 20 minutes");
        lines.ShouldContain("Servings: 4");
        lines.ShouldContain("Tags: Breakfast,Sweet");
        lines.ShouldContain("Created: 2024-01-15");
        lines.ShouldContain("Modified: 2024-02-01");

        text.IndexOf("===== RECIPE =====").ShouldBeLessThan(text.IndexOf("===== INGREDIENTS ====="));
        text.IndexOf("===== INGREDIENTS =====").ShouldBeLessThan(text.IndexOf("===== INSTRUCTIONS ====="));
        text.IndexOf("===== INSTRUCTIONS =====").ShouldBeLessThan(text.IndexOf("===== NOTES ====="));
        text.TrimEnd().ShouldEndWith("===== END RECIPE =====");
    }

    [Fact]
    public void ExportRecipeToText_Ingredients_AreSortedByNameWithUnitsAndModifiers()
    {
        var text = _service.ExportRecipeToText(CreateStoredRecipe());

        var lines = text.Split(Environment.NewLine);
        var start = Array.IndexOf(lines, "===== INGREDIENTS =====") + 1;
        lines.Skip(start).Take(3).ShouldBe(new[]
        {
            "2pieces Eggs (beaten)",
            "250g Flour",
            "473.176ml Milk",
        });
    }

    [Fact]
    public void ExportRecipeToText_IngredientWithoutQuantity_WritesNameOnly()
    {
        var recipe = CreateStoredRecipe();
        recipe.RecipeIngredients.Add(new RecipeIngredient
        {
            Ingredient = new Ingredient { Name = "Salt" },
            Modifier = "to taste",
        });

        var lines = _service.ExportRecipeToText(recipe).Split(Environment.NewLine);

        lines.ShouldContain("Salt (to taste)");
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("th-TH")]
    public void ExportRecipeToText_AnyCulture_UsesInvariantNumbersAndDates(string culture)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            var lines = _service.ExportRecipeToText(CreateStoredRecipe()).Split(Environment.NewLine);

            lines.ShouldContain("473.176ml Milk");
            lines.ShouldContain("Created: 2024-01-15");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ExportRecipeToText_NoTagsOrNotes_OmitsThoseSections()
    {
        var recipe = CreateStoredRecipe();
        recipe.Tags = "";
        recipe.Notes = "  ";

        var text = _service.ExportRecipeToText(recipe);

        text.ShouldNotContain("Tags:");
        text.ShouldNotContain("===== NOTES =====");
    }

    [Fact]
    public void ExportRecipesToText_MultipleRecipes_OrdersByName()
    {
        var b = CreateStoredRecipe();
        b.Name = "Banana Bread";
        var a = CreateStoredRecipe();
        a.Name = "Apple Pie";

        var text = _service.ExportRecipesToText(new List<Recipe> { b, a });

        text.IndexOf("Name: Apple Pie").ShouldBeLessThan(text.IndexOf("Name: Banana Bread"));
    }

    // ---------------------------------------------------------------- Round trip

    [Fact]
    public void ExportThenImport_StoredRecipe_RoundTripsAllFields()
    {
        var original = CreateStoredRecipe();

        var (recipes, errors) = _service.ParseRecipeText(_service.ExportRecipeToText(original));

        errors.ShouldBeEmpty();
        var imported = recipes.ShouldHaveSingleItem();
        imported.Name.ShouldBe(original.Name);
        imported.CookingTime.ShouldBe(original.CookingTime);
        imported.Servings.ShouldBe(original.Servings);
        imported.Tags.ShouldBe(original.Tags);
        imported.Instructions.ShouldBe(original.Instructions);
        imported.Notes.ShouldBe(original.Notes);
        imported.CreatedDate.ShouldBe(original.CreatedDate.Date);
        imported.ModifiedDate.ShouldBe(original.ModifiedDate.Date);

        var importedIngredients = imported.RecipeIngredients
            .Select(ri => (ri.Ingredient.Name, ri.Quantity, ri.Unit, ri.Modifier))
            .OrderBy(x => x.Name);
        var originalIngredients = original.RecipeIngredients
            .Select(ri => (ri.Ingredient.Name, ri.Quantity, ri.Unit, ri.Modifier))
            .OrderBy(x => x.Name);
        importedIngredients.ShouldBe(originalIngredients);
    }

    [Fact]
    public void ExportThenImport_MultipleRecipes_RoundTripsAll()
    {
        var a = CreateStoredRecipe();
        a.Name = "Apple Pie";
        var b = CreateStoredRecipe();
        b.Name = "Banana Bread";

        var (recipes, errors) = _service.ParseRecipeText(_service.ExportRecipesToText(new List<Recipe> { a, b }));

        errors.ShouldBeEmpty();
        recipes.Select(r => r.Name).ShouldBe(new[] { "Apple Pie", "Banana Bread" });
    }

    [Fact]
    public void ExportThenImport_IngredientWithoutQuantity_StaysWithoutQuantity()
    {
        var recipe = CreateStoredRecipe();
        recipe.RecipeIngredients.Add(new RecipeIngredient { Ingredient = new Ingredient { Name = "Salt" } });

        var (recipes, _) = _service.ParseRecipeText(_service.ExportRecipeToText(recipe));

        recipes.ShouldHaveSingleItem().RecipeIngredients
            .Single(ri => ri.Ingredient.Name == "Salt").Quantity.ShouldBeNull();
    }

    [Fact]
    public void ExportThenImport_NonEnglishCulture_RoundTripsDecimalQuantities()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var (recipes, _) = _service.ParseRecipeText(_service.ExportRecipeToText(CreateStoredRecipe()));

            recipes.ShouldHaveSingleItem().RecipeIngredients
                .Single(ri => ri.Ingredient.Name == "Milk").Quantity.ShouldBe(473.176m);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
