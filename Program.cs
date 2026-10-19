using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RecipesApp.Data;
using RecipesApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
// Blazor's SignalR hub keeps its default 32 KB message limit. File uploads (InputFile)
// stream in chunks, so they don't need a larger limit.
builder.Services.AddServerSideBlazor();

// Keep the data protection key ring next to the database (the Docker volume) so it survives
// container restarts. Blazor uses these keys for circuit IDs and for the encrypted browser
// storage that holds unsaved drafts; keys that vanish on restart would make drafts unreadable
// after every update. The folder is configurable so tests can use a temporary one.
var keysDirectory = Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration["DataProtection:KeysDirectory"] ?? Path.Combine("Data", "keys"));
builder.Services.AddDataProtection()
    .SetApplicationName("RecipesApp")
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));

// Add Entity Framework
builder.Services.AddDbContext<RecipeDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Services
builder.Services.AddScoped<RecipeService>();
// Resolve IRecipeReader to the same scoped RecipeService instance (and DbContext)
builder.Services.AddScoped<IRecipeReader>(sp => sp.GetRequiredService<RecipeService>());
builder.Services.AddScoped<IngredientService>();
builder.Services.AddScoped<RecipeImportExportService>();
builder.Services.AddScoped<GroceryListService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
else
{
    app.UseHttpsRedirection();
}
// Basic security headers on every response
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";      // browsers must use the declared type, never guess it
    headers.XFrameOptions = "DENY";               // the app can't be framed by another site (clickjacking)
    headers["Referrer-Policy"] = "no-referrer";   // don't leak recipe URLs to other sites
    await next();
});

app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapBlazorHub();

// API endpoint to serve recipe images separately for performance
app.MapGet("/api/recipe/{id}/image", async (int id, RecipeService recipeService, ILogger<Program> logger) =>
{
    var (imageData, _) = await recipeService.GetRecipeImageAsync(id);
    if (imageData == null)
    {
        return Results.NotFound();
    }

    // Serve only allowed image formats, with the content type detected from the bytes rather
    // than the stored one. This also covers images saved before uploads were validated.
    var contentType = ImageValidator.DetectContentType(imageData);
    if (contentType == null)
    {
        logger.LogWarning("Refused to serve image for recipe {RecipeId}: not an allowed image format", id);
        return Results.NotFound();
    }

    return Results.File(imageData, contentType);
});

app.MapFallbackToPage("/_Host");

// Create the database on first run, or upgrade one created by an older version
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
    await DatabaseInitializer.InitializeAsync(context, logger);
}

app.Run();

// Makes the compiler-generated Program class public so integration tests can use
// WebApplicationFactory<Program>.
public partial class Program { }
