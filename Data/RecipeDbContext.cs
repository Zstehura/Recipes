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
    public DbSet<Staple> Staples { get; set; }

    /// <summary>The staples a new database starts with; the cook can change them.</summary>
    public static readonly string[] DefaultStaples = ["Salt", "Black pepper", "Water", "Olive oil", "Vegetable oil"];

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
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Recipe)
                .WithMany(r => r.RecipeIngredients)
                .HasForeignKey(e => e.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Ingredient)
                .WithMany(i => i.RecipeIngredients)
                .HasForeignKey(e => e.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.Unit).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Modifier).HasMaxLength(100);
        });

        modelBuilder.Entity<Staple>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200).UseCollation("NOCASE");
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasData(DefaultStaples.Select((name, i) => new Staple { Id = i + 1, Name = name }));
        });
    }
}

