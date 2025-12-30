# Recipes App

A Blazor Server application for storing and managing recipes with SQLite database storage.

## Features

- Create, read, update, and delete recipes
- Search recipes by name, ingredients, or tags
- Recipe fields include:
  - Name
  - Ingredients
  - Instructions
  - Cooking Time (minutes)
  - Servings
  - Tags
  - Notes

## Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022, Visual Studio Code, or any IDE with .NET support

## Getting Started

1. Restore dependencies:
   ```
   dotnet restore
   ```

2. Run the application:
   ```
   dotnet run
   ```

3. Open your browser and navigate to:
   - HTTP: `http://localhost:5000`
   - HTTPS: `https://localhost:5001`

## Database

The application uses SQLite database stored in `Data/recipes.db`. The database will be automatically created when you first run the application.

## Project Structure

- `Models/` - Data models (Recipe)
- `Data/` - Database context (RecipeDbContext)
- `Services/` - Business logic (RecipeService)
- `Pages/` - Blazor pages (Index, RecipeForm, RecipeDetail)
- `Shared/` - Shared components (MainLayout, NavMenu)
- `wwwroot/` - Static files (CSS, etc.)

## Technologies Used

- Blazor Server
- Entity Framework Core
- SQLite
- Bootstrap 5
- Bootstrap Icons

