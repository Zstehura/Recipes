using NSubstitute;
using RecipesApp.Models;
using RecipesApp.Services;
using Shouldly;

namespace Recipes.Tests.Unit;

public class GroceryListServiceTests
{
    private static Recipe CreateRecipe(int id, params (string Name, decimal? Quantity, string? Unit, string? Modifier)[] ingredients)
    {
        var recipe = new Recipe { Id = id, Name = $"Recipe {id}" };
        foreach (var (name, quantity, unit, modifier) in ingredients)
        {
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                Ingredient = new Ingredient { Name = name },
                Quantity = quantity,
                Unit = Recipes.Tests.TestData.TestUnits.Parse(unit),
                Modifier = modifier,
            });
        }
        return recipe;
    }

    /// <summary>The items as the plain-text lines Copy and Share produce ("- 500 g Flour").</summary>
    private static string[] ItemLines(CombinedGroceryList groceryList) =>
        groceryList.Items.Select(i => $"- {i.ToText()}").ToArray();

    // ---------------------------------------------------------------- BuildGroceryList (pure)

    [Fact]
    public void BuildGroceryList_SameIngredientInTwoRecipes_SumsQuantities()
    {
        var recipes = new List<Recipe>
        {
            CreateRecipe(1, ("Flour", 200m, "g", null)),
            CreateRecipe(2, ("Flour", 300m, "g", null)),
        };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1, [2] = 1 });

        ItemLines(list).ShouldBe(new[] { "- 500 g Flour" });
    }

    [Fact]
    public void BuildGroceryList_LinesInDifferentUnits_AreConvertedBeforeAdding()
    {
        var recipes = new List<Recipe>
        {
            CreateRecipe(1, ("Flour", 1m, "Kilograms", null)),
            CreateRecipe(2, ("Flour", 500m, "g", null)),
            CreateRecipe(3, ("Milk", 2m, "Tablespoons", null), ("Milk", 100m, "ml", null)),
        };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1, [2] = 1, [3] = 1 });

        ItemLines(list).ShouldBe(new[] { "- 1.5 kg Flour", "- 129.57 ml Milk" });
    }

    [Fact]
    public void BuildGroceryList_Multiplier_ScalesQuantities()
    {
        var recipes = new List<Recipe> { CreateRecipe(1, ("Eggs", 2m, "pieces", null)) };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 3 });

        ItemLines(list).ShouldBe(new[] { "- 6 Eggs" });
    }

    [Fact]
    public void BuildGroceryList_DifferentMultipliersPerRecipe_AppliesEachSeparately()
    {
        var recipes = new List<Recipe>
        {
            CreateRecipe(1, ("Milk", 100m, "ml", null)),
            CreateRecipe(2, ("Milk", 50m, "ml", null)),
        };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 2, [2] = 4 });

        ItemLines(list).ShouldBe(new[] { "- 400 ml Milk" });
    }

    [Theory]
    [InlineData(999, "g", 1, "- 999 g Stock")]
    [InlineData(500, "g", 2, "- 1 kg Stock")]
    [InlineData(600, "g", 2, "- 1.2 kg Stock")]
    [InlineData(999, "ml", 1, "- 999 ml Stock")]
    [InlineData(750, "ml", 2, "- 1.5 L Stock")]
    public void BuildGroceryList_TotalOf1000OrMore_ConvertsToLargerUnit(
        decimal quantity, string baseUnit, int multiplier, string expectedLine)
    {
        var recipes = new List<Recipe> { CreateRecipe(1, ("Stock", quantity, baseUnit, null)) };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = multiplier });

        ItemLines(list).ShouldBe(new[] { expectedLine });
    }

    [Fact]
    public void BuildGroceryList_Quantity_IsRoundedToTwoDecimalPlaces()
    {
        var recipes = new List<Recipe> { CreateRecipe(1, ("Milk", 236.588m, "ml", null)) };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1 });

        ItemLines(list).ShouldBe(new[] { "- 236.59 ml Milk" });
    }

    [Fact]
    public void BuildGroceryList_SameIngredientDifferentBaseUnits_KeepsSeparateLines()
    {
        var recipes = new List<Recipe>
        {
            CreateRecipe(1, ("Butter", 100m, "g", null)),
            CreateRecipe(2, ("Butter", 2m, "pieces", null)),
            CreateRecipe(3, ("Butter", 50m, "g", null)),
            CreateRecipe(4, ("Butter", 1m, "pieces", null)),
        };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1, [2] = 1, [3] = 1, [4] = 1 });

        ItemLines(list).ShouldBe(new[] { "- 150 g Butter", "- 3 Butter" }, ignoreOrder: true);
    }

    [Fact]
    public void BuildGroceryList_SameIngredientDifferentModifiers_KeepsSeparateLines()
    {
        var recipes = new List<Recipe>
        {
            CreateRecipe(1, ("Tomato", 2m, "pieces", "sliced")),
            CreateRecipe(2, ("Tomato", 3m, "pieces", "diced")),
            CreateRecipe(3, ("Tomato", 1m, "pieces", null)),
        };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1, [2] = 1, [3] = 1 });

        ItemLines(list).ShouldBe(
            new[] { "- 2 Tomato (sliced)", "- 3 Tomato (diced)", "- 1 Tomato" },
            ignoreOrder: true);
    }

    [Fact]
    public void BuildGroceryList_IngredientNamesDifferingOnlyByCase_AreCombined()
    {
        var recipes = new List<Recipe>
        {
            CreateRecipe(1, ("flour", 100m, "g", null)),
            CreateRecipe(2, ("FLOUR", 100m, "g", null)),
        };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1, [2] = 1 });

        ItemLines(list).ShouldHaveSingleItem().ShouldBe("- 200 g Flour");
    }

    [Fact]
    public void BuildGroceryList_Items_AreSortedAlphabetically()
    {
        var recipes = new List<Recipe>
        {
            CreateRecipe(1, ("Sugar", 1m, "g", null), ("Apples", 1m, "pieces", null), ("Milk", 1m, "ml", null)),
        };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1 });

        ItemLines(list).ShouldBe(new[] { "- 1 Apples", "- 1 ml Milk", "- 1 g Sugar" });
    }

    [Fact]
    public void BuildGroceryList_MissingUnit_TreatsAsPieces()
    {
        var recipes = new List<Recipe> { CreateRecipe(1, ("Lemon", 2m, null, null)) };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1 });

        ItemLines(list).ShouldBe(new[] { "- 2 Lemon" });
    }

    [Fact]
    public void BuildGroceryList_NoQuantity_ListsNameOnly()
    {
        var recipes = new List<Recipe> { CreateRecipe(1, ("Salt", null, null, "to taste")) };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1 });

        ItemLines(list).ShouldBe(new[] { "- Salt (to taste)" });
    }

    [Fact]
    public void BuildGroceryList_QuantityBelowDisplayPrecision_ShowsTraceAmount()
    {
        var recipes = new List<Recipe> { CreateRecipe(1, ("Saffron", 0.004m, "g", null)) };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1 });

        ItemLines(list).ShouldBe(new[] { "- Saffron (trace amount)" });
    }

    [Theory]
    [InlineData("Cups", 1, 2, "- 3 cups Rice")]
    [InlineData("Tablespoons", 2, 2, "- 4 tbsp Oil")]
    [InlineData("Kilograms", 1, 0.5, "- 1.5 kg Flour")]
    [InlineData("Pounds", 1, 1, "- 2 lb Beef")]
    public void BuildGroceryList_AllLinesInTheSameUnit_KeepsThatUnit(string unit, double first, double second, string expected)
    {
        var name = expected.Split(' ').Last();
        var recipes = new List<Recipe>
        {
            CreateRecipe(1, (name, (decimal)first, unit, null)),
            CreateRecipe(2, (name, (decimal)second, unit, null)),
        };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1, [2] = 1 });

        ItemLines(list).ShouldBe(new[] { expected });
    }

    [Fact]
    public void BuildGroceryList_MixedUnits_AreAddedInBaseUnits()
    {
        var recipes = new List<Recipe>
        {
            CreateRecipe(1, ("Rice", 1m, "Cups", null)),
            CreateRecipe(2, ("Rice", 100m, "ml", null)),
        };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1, [2] = 1 });

        ItemLines(list).ShouldBe(new[] { "- 336.59 ml Rice" });
    }

    [Fact]
    public void BuildGroceryList_ReturnsItems_WithQuantityAndUnit()
    {
        var recipes = new List<Recipe> { CreateRecipe(1, ("Rice", 1.5m, "Cups", null), ("Salt", null, null, "to taste")) };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1 });

        list.RecipeCount.ShouldBe(1);
        list.Items.ShouldBe(new[]
        {
            new GroceryItem("Rice", 1.5m, MeasurementUnit.Cups),
            new GroceryItem("Salt (to taste)", null, null),
        });
        list.Items[0].Amount.ShouldBe("1.5 cups");
        list.Items[1].Amount.ShouldBeEmpty();
    }

    [Fact]
    public void Items_HaveDistinctKeys_EvenWithTheSameName()
    {
        var recipes = new List<Recipe> { CreateRecipe(1, ("Butter", 100m, "g", null), ("Butter", 2m, "pieces", null)) };

        var keys = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1 }).Items.Select(i => i.Key).ToList();

        keys.Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public void ToText_HasATitle_ThenOneLinePerItem()
    {
        var recipes = new List<Recipe> { CreateRecipe(1, ("Flour", 100m, "g", null), ("Eggs", 2m, "pieces", null)) };

        var text = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1 }).ToText();

        text.Split(Environment.NewLine).ShouldBe(new[] { "Grocery list", "", "- 2 Eggs", "- 100 g Flour" });
    }

    // ---------------------------------------------------------------- GenerateGroceryListAsync (with a fake reader)

    [Fact]
    public async Task GenerateGroceryListAsync_NoSelection_ReturnsAnEmptyList()
    {
        var reader = Substitute.For<IRecipeReader>();
        var service = new GroceryListService(reader);

        var result = await service.GenerateGroceryListAsync(new Dictionary<int, int>());

        result.Items.ShouldBeEmpty();
        result.RecipeCount.ShouldBe(0);
        await reader.DidNotReceiveWithAnyArgs().GetRecipeByIdAsync(default);
    }

    [Fact]
    public async Task GenerateGroceryListAsync_NullSelection_ReturnsAnEmptyList()
    {
        var service = new GroceryListService(Substitute.For<IRecipeReader>());

        var result = await service.GenerateGroceryListAsync(null);

        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateGroceryListAsync_AllRecipesMissing_ReturnsAnEmptyList()
    {
        var reader = Substitute.For<IRecipeReader>();
        reader.GetRecipeByIdAsync(Arg.Any<int>()).Returns((Recipe?)null);
        var service = new GroceryListService(reader);

        var result = await service.GenerateGroceryListAsync(new() { [42] = 1 });

        result.Items.ShouldBeEmpty();
        result.RecipeCount.ShouldBe(0);
    }

    [Fact]
    public async Task GenerateGroceryListAsync_SelectedRecipes_LoadsEachAndAggregates()
    {
        var reader = Substitute.For<IRecipeReader>();
        reader.GetRecipeByIdAsync(1).Returns(CreateRecipe(1, ("Flour", 200m, "g", null)));
        reader.GetRecipeByIdAsync(2).Returns(CreateRecipe(2, ("Flour", 100m, "g", null)));
        var service = new GroceryListService(reader);

        var result = await service.GenerateGroceryListAsync(new() { [1] = 1, [2] = 2 });

        ItemLines(result).ShouldBe(new[] { "- 400 g Flour" });
        await reader.Received(1).GetRecipeByIdAsync(1);
        await reader.Received(1).GetRecipeByIdAsync(2);
    }

    [Fact]
    public async Task GenerateGroceryListAsync_SomeRecipesMissing_SkipsMissingOnes()
    {
        var reader = Substitute.For<IRecipeReader>();
        reader.GetRecipeByIdAsync(1).Returns(CreateRecipe(1, ("Flour", 200m, "g", null)));
        reader.GetRecipeByIdAsync(99).Returns((Recipe?)null);
        var service = new GroceryListService(reader);

        var result = await service.GenerateGroceryListAsync(new() { [1] = 1, [99] = 1 });

        ItemLines(result).ShouldBe(new[] { "- 200 g Flour" });
    }
}
