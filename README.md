# Recipes

[![CI](https://github.com/Zstehura/Recipes/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Zstehura/Recipes/actions/workflows/ci.yml)
[![CodeQL](https://github.com/Zstehura/Recipes/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/Zstehura/Recipes/actions/workflows/codeql.yml)
[![codecov](https://codecov.io/gh/Zstehura/Recipes/branch/main/graph/badge.svg)](https://codecov.io/gh/Zstehura/Recipes)

A self-hosted recipe manager built with Blazor Server and SQLite. Store your recipes, manage ingredients, generate grocery lists, and search your collection by what you have on hand.

## Features

- **Recipe Management** - Create, edit, and delete recipes with ingredients, instructions, cooking time, servings, tags, notes, and images
- **Ingredient Tracking** - Manage a shared ingredient library with default units, renaming, and merging of duplicates
- **Search & Filtering** - Full-text search by name, ingredients, or tags with filters for cooking time, servings, and tags
- **Search by Available Ingredients** - Find recipes you can make with the ingredients you already have
- **Grocery List Generator** - Select recipes, set quantity multipliers, and get an aggregated shopping list with smart unit conversion
- **Import/Export** - Batch import and export recipes in a structured text format
- **Image Support** - Attach photos to recipes (up to 10MB each)

## Tech Stack

- .NET 8 / Blazor Server
- Entity Framework Core with SQLite
- Bootstrap 5 + Bootstrap Icons

## Deployment (Docker)

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) and [Docker Compose](https://docs.docker.com/compose/install/)

### Quick Start

1. Clone the repository:
   ```bash
   git clone https://github.com/Zstehura/Recipes.git
   cd Recipes
   ```

2. Build and start the container:
   ```bash
   docker compose up -d
   ```

3. Open `http://localhost:5000` in your browser (or `http://<server-ip>:5000` from another device on your network).

The database is stored in a `data/` directory on the host via a volume mount, so your recipes persist across container restarts and rebuilds.

### Updating

Pull the latest changes and rebuild:
```bash
git pull
docker compose up -d --build
```

### Configuration

The default port mapping is `5000:8080`. To change the host port, edit [docker-compose.yml](docker-compose.yml):

```yaml
ports:
  - "3000:8080"  # change 3000 to your desired port
```

## Local Development

### Prerequisites

- .NET 8.0 SDK

### Running

```bash
dotnet restore
dotnet run
```

The app will be available at `http://localhost:5000` (or `https://localhost:5001`).

The SQLite database is auto-created at `Data/recipes.db` on first run.

### Running Tests

```bash
dotnet test
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for running tests with Docker only, collecting coverage, and what CI checks.

## Project Structure

```
Models/          Data models (Recipe, Ingredient, RecipeIngredient)
Data/            EF Core DbContext and SQLite database
Services/        Business logic (recipes, ingredients, grocery list, import/export, unit conversion)
Pages/           Blazor pages (recipe list, form, detail, ingredients, search, grocery list, import/export)
Shared/          Layout and navigation components
wwwroot/         Static assets (CSS, Bootstrap)
tests/           xUnit test project (unit, SQLite data, and integration tests)
.github/         CI, CodeQL, and Dependabot configuration
```
