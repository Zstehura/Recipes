# Contributing

## Prerequisites

Either of these works:

- **.NET 10 SDK** installed locally, or
- **Docker** only (no SDK needed), using the official SDK image

## Build and test

With the .NET 10 SDK:

```bash
dotnet build
dotnet test
```

With Docker only:

```bash
docker run --rm -v "$PWD":/src -w /src \
  -v recipes-nuget:/root/.nuget/packages \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet test
```

The named `recipes-nuget` volume caches NuGet packages, so later runs are fast.

Tests never touch your real database. Data tests use a fresh in-memory SQLite database per test, and integration tests use a temporary file that is deleted afterwards.

### Code coverage

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
```

This writes `coverage.cobertura.xml` under `TestResults/`. To turn it into an HTML report:

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:CoverageReport -reporttypes:Html
```

Then open `CoverageReport/index.html`.

## Formatting

Formatting rules live in [.editorconfig](.editorconfig), and CI fails if the code doesn't match them. To check or fix locally:

```bash
dotnet format --verify-no-changes   # check
dotnet format                       # fix
```

## What CI checks

Every push and pull request to `main` runs [.github/workflows/ci.yml](.github/workflows/ci.yml):

1. Build in Release mode. Compiler warnings are errors.
2. `dotnet format --verify-no-changes`
3. All tests, with coverage
4. A Docker image build, followed by a smoke test that starts the image and checks the app, the Blazor script, and the stylesheet are served by the non-root user. The image isn't pushed anywhere.

[CodeQL](.github/workflows/codeql.yml) also scans for security issues, and [Dependabot](.github/dependabot.yml) opens pull requests for dependency updates.

## Conventions

- **Tests:** new or changed logic comes with tests in the same commit. Name tests `Method_Scenario_ExpectedResult` and follow Arrange-Act-Assert.
- **Test layout:** `tests/Recipes.Tests/Unit` holds pure logic, `Data` holds services against SQLite, and `Integration` holds HTTP-level tests. Shared helpers (test images, recipe text builder, capturing logger) live in `TestData`.
- **Known bugs:** a known bug is recorded as a test of the intended behavior, marked `Skip = "Bug #N: ..."`. Fixing the bug means removing the `Skip`.
- **Commits:** small, one logical change each, written in the imperative mood ("Add X", "Fix Y").

### Data and validation

- **Schema changes:** the database is created with `EnsureCreated`, which never changes an existing database. Any model change that alters the schema needs an upgrade step in `Data/DatabaseInitializer.cs`. Each step must check whether it's needed (so it can run on every startup), back up the database first if it rebuilds a table, and have tests that start from the old format. `DatabaseInitializerTests` shows the pattern, including the check that an upgraded schema matches a newly created one.
- **Validation:** field rules live as data annotations on the models and are enforced in `RecipeService` through `RecipeValidator`, so every save path is covered. Pages may check earlier for a better experience, but must not be the only check. Throw `ValidationException` with a message written for users.
- **Uploads:** validate content with `ImageValidator` (file signatures against an allow-list). Never trust the file name or the browser-reported content type.

### Errors and logging

- Catch specific exceptions. Show users only messages written for them (such as `ValidationException` messages); log anything unexpected with `ILogger` and show a generic message.
- Use message templates (`"Deleted recipe {RecipeId}"`), not string interpolation, so log fields stay structured.
- Log IDs and counts, never recipe or ingredient text. Recipes are personal data.
