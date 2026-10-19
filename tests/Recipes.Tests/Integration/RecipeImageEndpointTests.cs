using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RecipesApp.Data;
using RecipesApp.Models;
using Shouldly;

namespace Recipes.Tests.Integration;

/// <summary>
/// Runs the real ASP.NET Core pipeline in memory (no network, no browser) and calls the
/// image endpoint over HTTP. Each test class instance gets its own temporary SQLite file.
/// </summary>
public sealed class RecipeImageEndpointTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"recipes-test-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public RecipeImageEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_databasePath}"));
    }

    public void Dispose()
    {
        _factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    private int SeedRecipe(byte[]? image, string? contentType)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        var recipe = new Recipe
        {
            Name = "Bread",
            Instructions = "Bake.",
            CookingTime = 60,
            Servings = 1,
            ImageData = image,
            ImageContentType = contentType,
        };
        context.Recipes.Add(recipe);
        context.SaveChanges();
        return recipe.Id;
    }

    [Fact]
    public async Task GetImage_RecipeWithImage_ReturnsBytesWithStoredContentType()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var id = SeedRecipe(bytes, "image/png");
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/recipe/{id}/image");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("image/png");
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
    }

    [Fact]
    public async Task GetImage_RecipeWithoutImage_Returns404()
    {
        var id = SeedRecipe(null, null);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/recipe/{id}/image");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetImage_UnknownRecipe_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/recipe/999/image");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
