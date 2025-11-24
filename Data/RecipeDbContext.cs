using Microsoft.EntityFrameworkCore;
using RecipesApp.Models;

namespace RecipesApp.Data;

public class RecipeDbContext : DbContext
{
    public RecipeDbContext(DbContextOptions<RecipeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Recipe> Recipes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Ingredients).IsRequired();
            entity.Property(e => e.Instructions).IsRequired();
            entity.Property(e => e.CookingTime).IsRequired();
            entity.Property(e => e.Servings).IsRequired();
        });
    }
}

