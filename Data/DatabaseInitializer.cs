using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace RecipesApp.Data;

/// <summary>
/// Creates the database on first run and upgrades databases created by older versions.
/// </summary>
/// <remarks>
/// The app uses <c>EnsureCreated</c> rather than EF Core migrations. EnsureCreated builds the
/// schema for a new database but never changes an existing one, so every schema change since
/// the first release has a small upgrade step here. Each step checks whether it is needed
/// first, so running them on every startup is safe.
/// </remarks>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(RecipeDbContext context, ILogger logger)
    {
        await context.Database.EnsureCreatedAsync();

        await context.Database.OpenConnectionAsync();
        try
        {
            var connection = context.Database.GetDbConnection();
            await AddModifierColumnIfMissingAsync(connection, logger);
            await AddRecipeIngredientIdIfMissingAsync(connection, logger);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    // Upgrade 1: preparation modifier on recipe ingredients (e.g. "sliced").
    private static async Task AddModifierColumnIfMissingAsync(DbConnection connection, ILogger logger)
    {
        if (await ColumnExistsAsync(connection, "RecipeIngredients", "Modifier"))
            return;

        logger.LogInformation("Upgrading database: adding RecipeIngredients.Modifier");
        await ExecuteAsync(connection, null, "ALTER TABLE RecipeIngredients ADD COLUMN Modifier TEXT");
    }

    // Upgrade 2: replace the (RecipeId, IngredientId) primary key with an Id column, so the
    // same ingredient can appear more than once in a recipe. SQLite cannot change a table's
    // primary key, so the table is rebuilt and its rows copied, inside one transaction.
    private static async Task AddRecipeIngredientIdIfMissingAsync(DbConnection connection, ILogger logger)
    {
        if (await ColumnExistsAsync(connection, "RecipeIngredients", "Id"))
            return;

        logger.LogInformation("Upgrading database: adding a surrogate key to RecipeIngredients");
        await BackupAsync(connection, logger);

        await using var transaction = await connection.BeginTransactionAsync();

        // Must match what EnsureCreated produces for a new database (verified by tests)
        await ExecuteAsync(connection, transaction, """
            CREATE TABLE "RecipeIngredients_new" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RecipeIngredients" PRIMARY KEY AUTOINCREMENT,
                "RecipeId" INTEGER NOT NULL,
                "IngredientId" INTEGER NOT NULL,
                "Quantity" TEXT NULL,
                "Unit" TEXT NULL,
                "Modifier" TEXT NULL,
                CONSTRAINT "FK_RecipeIngredients_Ingredients_IngredientId" FOREIGN KEY ("IngredientId") REFERENCES "Ingredients" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_RecipeIngredients_Recipes_RecipeId" FOREIGN KEY ("RecipeId") REFERENCES "Recipes" ("Id") ON DELETE CASCADE
            )
            """);
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "RecipeIngredients_new" ("RecipeId", "IngredientId", "Quantity", "Unit", "Modifier")
            SELECT "RecipeId", "IngredientId", "Quantity", "Unit", "Modifier"
            FROM "RecipeIngredients"
            ORDER BY "RecipeId", "IngredientId"
            """);
        await ExecuteAsync(connection, transaction, """DROP TABLE "RecipeIngredients" """);
        await ExecuteAsync(connection, transaction, """ALTER TABLE "RecipeIngredients_new" RENAME TO "RecipeIngredients" """);
        await ExecuteAsync(connection, transaction, """CREATE INDEX "IX_RecipeIngredients_IngredientId" ON "RecipeIngredients" ("IngredientId")""");
        await ExecuteAsync(connection, transaction, """CREATE INDEX "IX_RecipeIngredients_RecipeId" ON "RecipeIngredients" ("RecipeId")""");

        await transaction.CommitAsync();
    }

    /// <summary>
    /// Copies the database to "&lt;file&gt;.backup-&lt;timestamp&gt;" before a schema change.
    /// VACUUM INTO writes a consistent copy even while the database is in WAL mode.
    /// </summary>
    private static async Task BackupAsync(DbConnection connection, ILogger logger)
    {
        var databasePath = connection.DataSource;
        if (string.IsNullOrEmpty(databasePath) || databasePath == ":memory:")
            return;

        var backupPath = $"{databasePath}.backup-{DateTime.Now:yyyyMMdd-HHmmss}";
        await using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $path";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$path";
        parameter.Value = backupPath;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync();

        logger.LogInformation("Database backed up to {BackupPath}", backupPath);
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column)
    {
        await using var command = connection.CreateCommand();
        // PRAGMA arguments can't be parameterized; the table name is a constant from this class
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static async Task ExecuteAsync(DbConnection connection, DbTransaction? transaction, string sql)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
