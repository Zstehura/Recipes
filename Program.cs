using Microsoft.EntityFrameworkCore;
using RecipesApp.Data;
using RecipesApp.Services;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB
});
builder.Services.AddServerSideBlazor();

// Configure form options to allow larger file uploads (10MB)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
});

// Add Entity Framework
builder.Services.AddDbContext<RecipeDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Services
builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<IngredientService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapBlazorHub();

// API endpoint to serve recipe images separately for performance
app.MapGet("/api/recipe/{id}/image", async (int id, RecipeService recipeService) =>
{
    var (imageData, contentType) = await recipeService.GetRecipeImageAsync(id);
    if (imageData == null || contentType == null)
    {
        return Results.NotFound();
    }
    return Results.File(imageData, contentType);
});

app.MapFallbackToPage("/_Host");

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
    context.Database.EnsureCreated();
}

app.Run();

