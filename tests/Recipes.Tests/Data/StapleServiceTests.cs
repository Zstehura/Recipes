using Microsoft.Extensions.Logging.Abstractions;
using RecipesApp.Data;
using RecipesApp.Services;
using Shouldly;

namespace Recipes.Tests.Data;

public sealed class StapleServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private StapleService CreateService() => new(_db.CreateContext(), NullLogger<StapleService>.Instance);

    private async Task<string[]> StapleNames() =>
        (await CreateService().GetStaplesAsync()).Select(s => s.Name).ToArray();

    [Fact]
    public async Task NewDatabase_StartsWithTheDefaultStaples_SortedByName()
    {
        (await StapleNames()).ShouldBe(RecipeDbContext.DefaultStaples.Order());
    }

    [Fact]
    public async Task AddStapleAsync_NamesItLikeAnIngredient()
    {
        var result = await CreateService().AddStapleAsync("  butter ");

        result.ShouldBe(AddStapleResult.Added);
        (await StapleNames()).ShouldContain("Butter");
    }

    [Theory]
    [InlineData("Salt")]
    [InlineData("salt")]
    [InlineData("SALT ")]
    public async Task AddStapleAsync_AlreadyListedInAnyCase_IsNotAddedTwice(string name)
    {
        var result = await CreateService().AddStapleAsync(name);

        result.ShouldBe(AddStapleResult.AlreadyListed);
        (await StapleNames()).Count(n => n.Equals("Salt", StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddStapleAsync_Blank_IsRejected(string name)
    {
        (await CreateService().AddStapleAsync(name)).ShouldBe(AddStapleResult.Empty);
        (await StapleNames()).Length.ShouldBe(RecipeDbContext.DefaultStaples.Length);
    }

    [Fact]
    public async Task AddStapleAsync_TooLong_IsRejected()
    {
        var service = CreateService();

        (await service.AddStapleAsync(new string('a', StapleService.MaxNameLength + 1))).ShouldBe(AddStapleResult.TooLong);
        (await service.AddStapleAsync(new string('a', StapleService.MaxNameLength))).ShouldBe(AddStapleResult.Added);
    }

    [Fact]
    public async Task RemoveStapleAsync_RemovesOnlyThatStaple()
    {
        var salt = (await CreateService().GetStaplesAsync()).Single(s => s.Name == "Salt");

        (await CreateService().RemoveStapleAsync(salt.Id)).ShouldBeTrue();

        (await StapleNames()).ShouldBe(RecipeDbContext.DefaultStaples.Where(n => n != "Salt").Order());
    }

    [Fact]
    public async Task RemoveStapleAsync_Missing_ReturnsFalse()
    {
        (await CreateService().RemoveStapleAsync(9999)).ShouldBeFalse();
    }
}
