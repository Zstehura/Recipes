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
                Unit = unit,
                Modifier = modifier,
            });
        }
        return recipe;
    }

    /// <summary>Returns only the ingredient lines ("- ...") from a generated list.</summary>
    private static string[] ItemLines(string groceryList) =>
        groceryList.Split(Environment.NewLine).Where(l => l.StartsWith("- ")).ToArray();

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
    public void BuildGroceryList_Multiplier_ScalesQuantities()
    {
        var recipes = new List<Recipe> { CreateRecipe(1, ("Eggs", 2m, "pieces", null)) };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 3 });

        ItemLines(list).ShouldBe(new[] { "- 6 piece(s) Eggs" });
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

        ItemLines(list).ShouldBe(new[] { "- 150 g Butter", "- 3 piece(s) Butter" }, ignoreOrder: true);
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
            new[] { "- 2 piece(s) Tomato (sliced)", "- 3 piece(s) Tomato (diced)", "- 1 piece(s) Tomato" },
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

        ItemLines(list).ShouldBe(new[] { "- 1 piece(s) Apples", "- 1 ml Milk", "- 1 g Sugar" });
    }

    [Fact]
    public void BuildGroceryList_MissingUnit_TreatsAsPieces()
    {
        var recipes = new List<Recipe> { CreateRecipe(1, ("Lemon", 2m, null, null)) };

        var list = GroceryListService.BuildGroceryList(recipes, new() { [1] = 1 });

        ItemLines(list).ShouldBe(new[] { "- 2 piece(s) Lemon" });
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
    [InlineData(1, "Grocery List for 1 Recipe", "Total: 1 ingredient from 1 recipe")]
    [InlineData(2, "Grocery List for 2 Recipes", "Total: 1 ingredient from 2 recipes")]
    public void BuildGroceryList_HeaderAndFooter_ArePluralizedByCount(int recipeCount, string expectedHeader, string expectedFooter)
    {
        var recipes = Enumerable.Range(1, recipeCount)
            .Select(id => CreateRecipe(id, ("Flour", 100m, "g", null)))
            .ToList();
        var multipliers = recipes.ToDictionary(r => r.Id, _ => 1);

        var lines = GroceryListService.BuildGroceryList(recipes, multipliers).Split(Environment.NewLine);

        lines.First().ShouldBe(expectedHeader);
        lines.Last().ShouldBe(expectedFooter);
    }

    // ---------------------------------------------------------------- GenerateGroceryListAsync (with a fake reader)

    [Fact]
    public async Task GenerateGroceryListAsync_NoSelection_ReturnsNoRecipesMessage()
    {
        var reader = Substitute.For<IRecipeReader>();
        var service = new GroceryListService(reader);

        var result = await service.GenerateGroceryListAsync(new Dictionary<int, int>());

        result.ShouldBe("No recipes selected.");
        await reader.DidNotReceiveWithAnyArgs().GetRecipeByIdAsync(default);
    }

    [Fact]
    public async Task GenerateGroceryListAsync_NullSelection_ReturnsNoRecipesMessage()
    {
        var service = new GroceryListService(Substitute.For<IRecipeReader>());

        var result = await service.GenerateGroceryListAsync(null!);

        result.ShouldBe("No recipes selected.");
    }

    [Fact]
    public async Task GenerateGroceryListAsync_AllRecipesMissing_ReturnsNoValidRecipesMessage()
    {
        var reader = Substitute.For<IRecipeReader>();
        reader.GetRecipeByIdAsync(Arg.Any<int>()).Returns((Recipe?)null);
        var service = new GroceryListService(reader);

        var result = await service.GenerateGroceryListAsync(new() { [42] = 1 });

        result.ShouldBe("No valid recipes found.");
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
