using System.ComponentModel.DataAnnotations;
using RecipesApp.Models;
using RecipesApp.Services;
using Shouldly;

namespace Recipes.Tests.Unit;

public class RecipeValidatorTests
{
    private static Recipe ValidRecipe() => new()
    {
        Name = "Pancakes",
        Instructions = "Mix and cook.",
        CookingTime = 20,
        Servings = 4,
    };

    private static IngredientEntry ValidEntry() => new() { Name = "Flour", Quantity = 250, Unit = MeasurementUnit.Grams };

    [Fact]
    public void Validate_ValidRecipeAndEntries_DoesNotThrow()
    {
        Should.NotThrow(() => RecipeValidator.Validate(ValidRecipe(), new[] { ValidEntry() }));
    }

    [Fact]
    public void Validate_EntryWithoutQuantity_IsValid()
    {
        var entry = new IngredientEntry { Name = "Salt", Quantity = null };

        Should.NotThrow(() => RecipeValidator.Validate(ValidRecipe(), new[] { entry }));
    }

    public static TheoryData<Action<Recipe>, string> InvalidRecipes => new()
    {
        { r => r.Name = "", "Recipe name is required" },
        { r => r.Name = new string('a', 201), "Recipe name cannot exceed 200 characters" },
        { r => r.Instructions = "", "Instructions are required" },
        { r => r.Instructions = new string('a', 20001), "Instructions cannot exceed 20,000 characters" },
        { r => r.Notes = new string('a', 10001), "Notes cannot exceed 10,000 characters" },
        { r => r.Tags = new string('a', 501), "Tags cannot exceed 500 characters" },
        { r => r.CookingTime = 0, "Cooking time must be at least 1 minute" },
        { r => r.Servings = -1, "Servings must be at least 1" },
    };

    [Theory]
    [MemberData(nameof(InvalidRecipes))]
    public void Validate_InvalidRecipeField_ThrowsWithMessage(Action<Recipe> makeInvalid, string expectedMessage)
    {
        var recipe = ValidRecipe();
        makeInvalid(recipe);

        var ex = Should.Throw<ValidationException>(() => RecipeValidator.Validate(recipe, Array.Empty<IngredientEntry>()));

        ex.Message.ShouldBe(expectedMessage);
    }

    public static TheoryData<Action<IngredientEntry>, string> InvalidEntries => new()
    {
        { e => e.Quantity = -1, "Quantity must be between 0 and 1,000,000" },
        { e => e.Quantity = 1_000_001, "Quantity must be between 0 and 1,000,000" },
        { e => e.Modifier = new string('a', 101), "Preparation note cannot exceed 100 characters" },
        { e => e.Unit = (MeasurementUnit)999, "Unknown unit" },
    };

    [Theory]
    [MemberData(nameof(InvalidEntries))]
    public void Validate_InvalidEntry_ThrowsNamingTheIngredient(Action<IngredientEntry> makeInvalid, string expectedMessage)
    {
        var entry = ValidEntry();
        makeInvalid(entry);

        var ex = Should.Throw<ValidationException>(() => RecipeValidator.Validate(ValidRecipe(), new[] { entry }));

        ex.Message.ShouldBe($"Ingredient \"Flour\": {expectedMessage}");
    }

    [Fact]
    public void Validate_EntryNameTooLong_ShortensNameInMessage()
    {
        var entry = new IngredientEntry { Name = new string('x', 300) };

        var ex = Should.Throw<ValidationException>(() => RecipeValidator.Validate(ValidRecipe(), new[] { entry }));

        ex.Message.ShouldBe($"Ingredient \"{new string('x', 40)}...\": Name cannot exceed 200 characters");
    }
}
