# Contributing

## Prerequisites

Either of these works:

- **.NET 8 SDK** installed locally, or
- **Docker** only (no SDK needed), using the official SDK image

## Build and test

With the .NET 8 SDK:

```bash
dotnet build
dotnet test
```

With Docker only:

```bash
docker run --rm -v "$PWD":/src -w /src \
  -v recipes-nuget:/root/.nuget/packages \
  mcr.microsoft.com/dotnet/sdk:8.0 dotnet test
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
4. A Docker image build. The image is built but not pushed.

[CodeQL](.github/workflows/codeql.yml) also scans for security issues, and [Dependabot](.github/dependabot.yml) opens pull requests for dependency updates.

## Conventions

- **Tests:** new or changed logic comes with tests in the same commit. Name tests `Method_Scenario_ExpectedResult` and follow Arrange-Act-Assert.
- **Test layout:** `tests/Recipes.Tests/Unit` holds pure logic, `Data` holds services against SQLite, and `Integration` holds HTTP-level tests.
- **Known bugs:** a known bug is recorded as a test of the intended behavior, marked `Skip = "Bug #N: ..."`. Fixing the bug means removing the `Skip`.
- **Commits:** small, one logical change each, written in the imperative mood ("Add X", "Fix Y").
