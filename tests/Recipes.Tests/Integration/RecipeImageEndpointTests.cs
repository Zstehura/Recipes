using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recipes.Tests.TestData;
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
    private readonly string _keysDirectory = Path.Combine(Path.GetTempPath(), $"recipes-test-keys-{Guid.NewGuid():N}");
    private readonly WebApplicationFactory<Program> _factory;

    public RecipeImageEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_databasePath}")
            .UseSetting("DataProtection:KeysDirectory", _keysDirectory));
    }

    public void Dispose()
    {
        _factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
        if (Directory.Exists(_keysDirectory))
        {
            Directory.Delete(_keysDirectory, recursive: true);
        }
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
        var bytes = TestImages.Png;
        var id = SeedRecipe(bytes, "image/png");
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/recipe/{id}/image");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("image/png");
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
    }

    [Fact]
    public async Task GetImage_StoredSvgFromBeforeValidation_Returns404()
    {
        // Seeded directly into the database, as an image saved before uploads were validated
        var id = SeedRecipe(TestImages.Svg, "image/svg+xml");
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/recipe/{id}/image");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetImage_WrongStoredContentType_ServesTypeDetectedFromBytes()
    {
        var id = SeedRecipe(TestImages.Jpeg, "text/html");
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/recipe/{id}/image");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("image/jpeg");
    }

    [Fact]
    public async Task UnhandledError_InProduction_ShowsGenericErrorPageWithoutDetails()
    {
        using var production = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var client = production.CreateClient();
        await client.GetAsync("/");   // start the app so the database is created
        using (var scope = production.Services.CreateScope())
        {
            // Break the database so the next request throws
            scope.ServiceProvider.GetRequiredService<RecipeDbContext>().Database.ExecuteSqlRaw("DROP TABLE RecipeIngredients; DROP TABLE Recipes;");
        }

        var response = await client.GetAsync("/api/recipe/1/image");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        body.ShouldContain("Something went wrong");
        body.ShouldNotContain("SqliteException");
        body.ShouldNotContain("no such table");
        await ShouldAllLoadAsync(client, body, "/Error");
    }

    [Fact]
    public async Task HostPage_EveryLinkedFile_Loads()
    {
        var client = _factory.CreateClient();
        var body = await client.GetStringAsync("/");

        await ShouldAllLoadAsync(client, body, "/");
    }

    // Every stylesheet, script, icon, and manifest a page links to must exist, so moving a file
    // can't silently leave a page unstyled (the error page once pointed at a removed stylesheet)
    private static async Task ShouldAllLoadAsync(HttpClient client, string html, string page)
    {
        var urls = System.Text.RegularExpressions.Regex
            .Matches(html, "<(?:link|script)\\b[^>]*?\\b(?:href|src)=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .Where(u => !u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .ToList();
        urls.ShouldNotBeEmpty();

        foreach (var url in urls)
        {
            var response = await client.GetAsync(url.StartsWith('/') ? url : "/" + url);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{page} links to {url}");
        }
    }

    [Fact]
    public async Task DataProtectionKeys_ArePersistedToConfiguredDirectory()
    {
        var client = _factory.CreateClient();

        // Rendering the Blazor host page protects the circuit descriptor, which creates a key
        await client.GetAsync("/");

        Directory.GetFiles(_keysDirectory, "key-*.xml").ShouldNotBeEmpty();
    }

    [Fact]
    public void DisconnectedCircuits_AreRetainedFor30Minutes()
    {
        var options = _factory.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Components.Server.CircuitOptions>>()
            .Value;

        options.DisconnectedCircuitRetentionPeriod.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task HostPage_IncludesReconnectionBannerAndDraftScripts()
    {
        var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/");

        html.ShouldContain("id=\"components-reconnect-modal\"");
        html.ShouldContain("js/drafts.js");
        html.ShouldContain("js/reconnect.js");
        (await client.GetAsync("/js/reconnect.js")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/api/recipe/1/image")]
    public async Task AnyResponse_IncludesSecurityHeaders(string url)
    {
        SeedRecipe(TestImages.Png, "image/png");
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(new[] { "nosniff" });
        response.Headers.GetValues("X-Frame-Options").ShouldBe(new[] { "DENY" });
        response.Headers.GetValues("Referrer-Policy").ShouldBe(new[] { "no-referrer" });
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
