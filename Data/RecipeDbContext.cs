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
    public DbSet<Ingredient> Ingredients { get; set; }
    public DbSet<RecipeIngredient> RecipeIngredients { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Recipe entity
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Instructions).IsRequired();
            entity.Property(e => e.CookingTime).IsRequired();
            entity.Property(e => e.Servings).IsRequired();
            // Configure ImageData as BLOB (SQLite stores byte[] as BLOB automatically)
            entity.Property(e => e.ImageData).HasColumnType("BLOB");
            entity.Property(e => e.ImageContentType).HasMaxLength(100);
        });

        // Configure Ingredient entity
        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200).UseCollation("NOCASE");
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.DefaultUnit)
                .HasConversion<int>()
                .IsRequired();
        });

        // Configure RecipeIngredient join table
        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasKey(e => new { e.RecipeId, e.IngredientId });

            entity.HasOne(e => e.Recipe)
                .WithMany(r => r.RecipeIngredients)
                .HasForeignKey(e => e.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Ingredient)
                .WithMany(i => i.RecipeIngredients)
                .HasForeignKey(e => e.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.Unit).HasMaxLength(20);
            entity.Property(e => e.Modifier).HasMaxLength(100);
        });
    }
}

