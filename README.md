# Home Recipe Repo

[![CI](https://github.com/Zstehura/Recipes/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Zstehura/Recipes/actions/workflows/ci.yml)
[![CodeQL](https://github.com/Zstehura/Recipes/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/Zstehura/Recipes/actions/workflows/codeql.yml)
[![codecov](https://codecov.io/gh/Zstehura/Recipes/branch/main/graph/badge.svg)](https://codecov.io/gh/Zstehura/Recipes)

Home Recipe Repo is a self-hosted recipe manager built with .NET 10, Blazor Server, and SQLite. Store your recipes, manage a shared ingredient library, generate a combined grocery list, and find recipes by what you have on hand. It runs as a single Docker container on a home server.

The app was renamed from "Recipes" to Home Recipe Repo. The repository, solution, project, and C# namespace (`RecipesApp`) keep the original name, since renaming them would touch every file for no change users can see.

## Contents

- [Features](#features)
- [Screenshots](#screenshots)
- [Tech stack](#tech-stack)
- [Getting started (Docker)](#getting-started-docker)
- [Local development](#local-development)
- [Architecture](#architecture)
- [Testing](#testing)
- [Design decisions](#design-decisions)
- [Security](#security)
- [Known limitations](#known-limitations)
- [Import format](#import-format)

## Features

- **Built for phones first:** a bottom navigation bar, large touch targets, and layouts that work one-handed at 390 px wide, while still using the extra space on a desktop
- **Recipes:** create, edit, and delete recipes with ingredients, instructions, cooking time, servings, tags, notes, and a photo. Deleting asks for confirmation first
- **Cooking view:** a large, readable recipe page where you tap ingredients to cross them off as you go. The ticks survive a reload or switching apps
- **Ingredient library:** one shared list of ingredients, with default units, renaming, and merging of duplicates. Amounts keep the unit they were entered in ("2 tbsp", "1.5 cups")
- **Search and filtering:** search by name, ingredient, or tag, and filter by cooking time, servings, and tags
- **Search by ingredient:** pick ingredients and find the recipes that use all of them, or switch to "Only these" to see what you can make from what you have. Staples you always keep (salt, oil, and any you add) don't need to be picked
- **Grocery list:** pick recipes, set how many batches of each, and get one combined list. Quantities are added up across recipes: amounts entered in the same unit stay in it ("4 tbsp"), and mixed units are converted and shown readably (1.2 kg rather than 1200 g). Tick items off as they go in the basket (kept on the device), and copy the list or share it straight to another app where the browser supports it
- **Import and export:** move recipes in and out as a plain-text format (see [Import format](#import-format))
- **Unsaved work survives:** a half-written recipe, the grocery list, and search filters come back after switching apps on a phone, a lost connection, a page reload, or a server restart
- **Home-screen icon:** a web app manifest and icons, so a phone can add the app to its home screen and open it without the browser bar

## Screenshots

Phone-width (390 px) captures from before and after the mobile-first rework.

> **TODO (owner):** add screenshots under `docs/screenshots/` and replace each placeholder below with an image link, e.g. `![Recipe list, after](docs/screenshots/after/recipe-list.png)`.

| Screen | Before | After |
|---|---|---|
| Recipe list | _Placeholder: `docs/screenshots/before/recipe-list.png`_ | _Placeholder: `docs/screenshots/after/recipe-list.png`_ |
| Recipe page (cooking view) | _Placeholder: `docs/screenshots/before/recipe-detail.png`_ | _Placeholder: `docs/screenshots/after/recipe-detail.png`_ |
| Recipe form | _Placeholder: `docs/screenshots/before/recipe-form.png`_ | _Placeholder: `docs/screenshots/after/recipe-form.png`_ |
| Grocery list | _Placeholder: `docs/screenshots/before/grocery-list.png`_ | _Placeholder: `docs/screenshots/after/grocery-list.png`_ |
| Search by ingredient | _Placeholder: `docs/screenshots/before/search-by-ingredients.png`_ | _Placeholder: `docs/screenshots/after/search-by-ingredients.png`_ |
| Ingredients | _Placeholder: `docs/screenshots/before/ingredients.png`_ | _Placeholder: `docs/screenshots/after/ingredients.png`_ |

## Tech stack

| Area | Choice |
|---|---|
| Runtime | .NET 10 (LTS) |
| UI | Blazor Server, Bootstrap 5, Bootstrap Icons |
| Data | Entity Framework Core 10 with SQLite |
| Tests | xUnit, bUnit, NSubstitute, Shouldly, coverlet, `WebApplicationFactory` |
| CI and security | GitHub Actions, CodeQL, Dependabot, Codecov |
| Hosting | Docker (multi-stage build, chiseled non-root runtime image), Docker Compose |

## Getting started (Docker)

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) and [Docker Compose](https://docs.docker.com/compose/install/)

### Quick start

1. Clone the repository:
   ```bash
   git clone https://github.com/Zstehura/Recipes.git
   cd Recipes
   ```

2. Create the data folder and give it to the container's user. The app runs as a non-root user (UID 1654), so it needs write access to this folder:
   ```bash
   mkdir -p data
   sudo chown 1654:1654 data
   ```
   (On Docker Desktop for macOS or Windows, `mkdir -p data` is enough.)

3. Build and start the container:
   ```bash
   docker compose up -d
   ```

4. Open `http://localhost:5000` in your browser (or `http://<server-ip>:5000` from another device on your network).

The database is a single SQLite file in the `data/` folder on the host, mounted into the container, so your recipes persist across container restarts and rebuilds. To back up, copy that folder.

### Updating

Pull the latest changes and rebuild:
```bash
git pull
docker compose up -d --build
```

**Upgrading from a version that ran as root** (before the non-root image): the existing `data/` folder is owned by root, so give it to the container's user once, before rebuilding:
```bash
sudo chown -R 1654:1654 data
```
If the container then fails to start with "unable to open database file", the folder ownership is the cause.

When a new version changes the database structure, the app upgrades the database automatically on startup and first writes a backup next to it (`data/recipes.db.backup-<timestamp>`). Once you've confirmed everything works, you can delete the backup.

### Configuration

The default port mapping is `5000:8080`. To change the host port, edit [docker-compose.yml](docker-compose.yml):

```yaml
ports:
  - "3000:8080"  # change 3000 to your desired port
```

## Local development

### Prerequisites

- .NET 10 SDK, or Docker only (see [CONTRIBUTING.md](CONTRIBUTING.md) for running everything in the SDK container)

### Running

```bash
dotnet run
```

The app is available at `http://localhost:5000`. The SQLite database is created at `Data/recipes.db` on first run.

### Running tests

```bash
dotnet test
```

[CONTRIBUTING.md](CONTRIBUTING.md) covers running tests with Docker only, collecting coverage, formatting, and what CI checks.

## Architecture

```mermaid
flowchart LR
    Browser["Browser"] <-->|"SignalR (WebSocket)"| Pages["Blazor pages<br/>Pages/, Shared/"]
    Browser -->|"HTTP GET"| ImageEndpoint["Image endpoint<br/>/api/recipe/{id}/image"]
    Pages --> Services["Services<br/>business logic"]
    ImageEndpoint --> Services
    Services --> EF["EF Core<br/>RecipeDbContext"]
    EF --> DB[("SQLite file<br/>data/recipes.db")]
```

Blazor Server runs the UI on the server. The browser keeps a SignalR connection open, sends UI events over it, and receives rendering updates back. Pages call services directly, with no separate HTTP API in between. The one exception is recipe images, which are served by a plain HTTP endpoint so the browser can load and cache them like any other image.

```
Models/          Entities (Recipe, Ingredient, RecipeIngredient, Staple) and the IngredientEntry form model
Data/            RecipeDbContext and DatabaseInitializer (creates and upgrades the database)
Services/        Business logic:
                   RecipeService, IngredientService        recipe and ingredient data access
                   StapleService                           the staples list for "Only these" searches
                   GroceryListService                      combining recipes into a grocery list
                   RecipeImportExportService               the text import/export format
                   UnitConversionService                   unit conversion to and from base units
                   RecipeValidator, ImageValidator         server-side validation
                   RecipeTags, IngredientEntryMapper       small shared helpers
Services/Drafts/ Unsaved page state kept in encrypted browser storage (DraftService, IDraftStore),
                 including the cooking view's crossed-off ingredients (CookingProgress)
Pages/           Blazor pages, plus the error page
Shared/          Layout, navigation (AppNav, NavMenu), and shared components:
                   ConfirmDialog, OverflowMenu             confirmation dialog and "⋯" action menu
                   RecipeCard, RecipeFilterPanel           recipe list item and the shared filters
                   QuantityStepper, DraftNotice            batch count control, draft restore notice
wwwroot/         Static assets: CSS, small scripts in js/, Bootstrap and Bootstrap Icons in lib/,
                 the web app manifest and icons/
tests/           xUnit test project (see Testing)
.github/         CI, CodeQL, and Dependabot configuration
```

## Testing

```bash
dotnet test
```

The suite has over 500 tests and runs in about a second. CI runs it on every push and pull request, along with a formatting check and a Docker image build that is then started and smoke-tested.

### Strategy

| Layer | Folder | What it tests | How |
|---|---|---|---|
| Unit | `tests/Recipes.Tests/Unit` | Pure logic: unit conversion, import/export parsing, grocery list totals, validation, tag parsing, navigation and filter state, and the web app manifest against its icon files | Plain inputs and outputs, mostly data-driven (`[Theory]`). No database |
| Data | `tests/Recipes.Tests/Data` | `RecipeService`, `IngredientService`, and the database upgrade | A real SQLite database in memory, created fresh for each test |
| Component | `tests/Recipes.Tests/Components` | Each main page and the shared components: what it shows, labels on every field, navigation, confirmations, focus moves, and saving, restoring, and clearing drafts | bUnit renders the real pages against the SQLite test database, with an in-memory draft store in place of browser storage and faked browser calls |
| Integration | `tests/Recipes.Tests/Integration` | The image endpoint, security headers, and the production error page | The full ASP.NET Core pipeline in memory via `WebApplicationFactory`, with a temporary database file |

Principles:

- **Arrange-Act-Assert**, with names like `Method_Scenario_ExpectedResult`.
- **Isolation:** every test gets its own database, so tests never depend on each other or on order.
- **Failure paths, not just the happy path:** malformed and hostile input, invalid images, oversized fields, missing records, and legacy database formats.
- **Mocks only at boundaries.** `GroceryListService` depends on a small `IRecipeReader` interface, which tests replace with an NSubstitute fake. Services whose job is talking to the database are tested against a real database instead of a mocked `DbContext`.
- **Tests are checked by breaking the code.** For each UI change, a few deliberate bugs were introduced to confirm the new tests fail, then removed.
- **Bugs get a test first.** Each bug found during the testing work was recorded as a failing test of the intended behavior, then fixed, so the test now guards against regressions.

### What is and isn't covered

Covered (about 95–100% line coverage per service):
- All services and helpers in `Services/`
- The database upgrade path, including a check that an upgraded database has exactly the same schema as a newly created one
- The image endpoint, the security headers, and the error page

Not covered by automated tests:
- **Looks and layout.** Component tests check structure and behavior, not how things look. Each page was reviewed in screenshots at phone width instead, and the main pages at desktop width too.
- **Browser-only behavior,** such as the native dialog's focus trap and Escape key, copying and sharing, downloading files, and the small scripts in `wwwroot/js` (committing a field when a phone hides the page, the reconnect banner). Component tests fake these calls. The real behavior was checked by hand in Chrome, including copying over plain HTTP. The end-to-end behavior was checked in headless Chrome against the real image: frozen page and network cut for 1 and 4 minutes, a server restart, and a reload.
- **The running container, beyond a smoke test.** CI starts the built image and checks that the page, the Blazor script, the stylesheets, the manifest, and the icons load, and that it runs as the non-root user, but doesn't exercise the UI in a browser.

## Design decisions

**Blazor Server.** The whole app, UI included, is written in C#, with no separate front-end build and no API layer to maintain. For a single-household app on a home network, the main cost of Blazor Server, which is one open connection and some server memory per browser tab, doesn't matter. The trade-off is that page state lives in server memory and the UI depends on that connection, which phones drop whenever they suspend a background tab. Drafts handle that (next decision).

**SQLite.** One file, no database server to run or secure, and a backup is a file copy. SQLite allows only one writer at a time, which is no constraint for a few users.

**Real SQLite in memory for tests, not EF Core's InMemory provider.** The InMemory provider isn't a relational database: it doesn't enforce primary keys, unique indexes, foreign keys, cascades, or collations, and it doesn't translate LINQ to SQL. Several bugs in this project, such as a unique-key violation when a recipe listed the same ingredient twice, only show up against a real relational database. SQLite in memory gives that realism at unit-test speed.

**The Docker volume.** `docker-compose.yml` mounts the host's `data/` folder at `/app/Data`, which is where the app keeps its database. The container itself holds no state, so it can be rebuilt or replaced freely. The runtime image runs as a non-root user, so that folder must be writable by UID 1654 (see [Getting started](#getting-started-docker)).

**Database upgrades without EF migrations.** The database is created with `EnsureCreated`, which builds a new database but never changes an existing one. Each schema change since then has a small, idempotent upgrade step in `Data/DatabaseInitializer.cs` that runs at startup. Steps that change existing data back up the database first (steps that only add something, like the staples table, don't need to), and each one is covered by tests against the old format. EF Core migrations are the standard tool and the natural next step if the schema keeps changing; adopting them would mean baselining existing databases, which wasn't worth the risk for the four small changes so far.

**Quantities stored as entered, converted only to add them up.** A line is stored with the quantity and unit it was entered in ("2 tbsp"), so the recipe page, the edit form, and exports show it the way it was written. The grocery list converts each line to grams, milliliters, or pieces when it adds amounts up across recipes, which is the only place a common unit is needed. Earlier versions stored everything in base units, which made the totals simple but showed "2 tbsp" as "29.57 ml" everywhere else. Recipes saved before the change keep their base-unit amounts (the original units can't be recovered), shown rounded to two decimal places. The unit is stored by name in the existing text column, so the upgrade only renamed "g", "ml", and "pieces"; the schema didn't change.

**"Only these" search with an editable staples list.** "What can I make" means every ingredient of a recipe is one you picked, but nobody picks salt. Staples are a short list of names, editable on the Find page, that count as on hand. A recipe matches when each of its ingredients is picked or a staple and at least one is picked, so recipes made only of staples don't match every search. Staples are matched to ingredients by name, ignoring case, so one can be listed before any recipe uses it. A separate list was chosen over a flag on each ingredient because it keeps the Ingredients page simple and works for ingredients that don't exist yet.

**Images stored in the database.** Photos are BLOBs in SQLite, so the database file is a complete backup with nothing else to keep in sync, and no user-supplied file names ever reach the file system. List and detail queries exclude the image bytes, and images are served separately by recipe ID.

**Unsaved work: browser drafts, plus longer circuit retention.** Reproducing the "lost work when I switch apps" bug showed the cause: Blazor Server keeps a disconnected tab's state (its circuit) for only 3 minutes by default, then deletes it. The fix has two layers:
- *Drafts.* Pages with valuable input (recipe form, grocery list) save it after every change to the browser's local storage. Search filters and the cooking view's crossed-off ingredients go to session storage, so they last only for the tab. Crossed-off ingredients are dropped if the recipe has been edited since. Values are encrypted and signed with ASP.NET Core's data protection keys, which are now kept with the database so drafts stay readable after an update. Drafts expire after 7 days, are cleared on save or cancel, and anything corrupt or out of date is quietly discarded. An edit draft is dropped if the recipe was saved since. The logic sits behind `IDraftStore`, so it's unit-tested without a browser.
- *Longer retention.* Disconnected circuits are kept for 30 minutes instead of 3, so short absences reconnect seamlessly. Each retained circuit holds one tab's page state in server memory, a negligible cost at household scale (capped at 100 circuits).

Alternatives considered:
- **Blazor WebAssembly or Auto mode** would keep state in the browser and avoid the connection problem entirely, but it means adding an authenticated HTTP API for every operation, downloading the .NET runtime to the phone, and rewriting data access. It's a much larger change, and it still wouldn't survive the phone discarding the tab without the same kind of browser storage. Not worth it for this app.
- **.NET 10's built-in circuit state persistence** (`[PersistentState]`) restores state after the server evicts a circuit, but not after a reload, a tab discard, or a server restart. Drafts cover all of those with one mechanism. Restoring drafts when a page initializes also covers .NET 10's automatic circuit resume, so the app doesn't need both.

**Mobile-first UI with plain Bootstrap.** The app is mostly used on a phone in the kitchen or the shop, so each page was redesigned for a 390 px screen first. The main pieces:
- *Bottom navigation* for the four places used most (Recipes, Find, Grocery, More), and a top bar on wider screens. Rarely used pages (Ingredients, Import and export) are under More.
- *One action per screen in reach of the thumb.* The main button on the grocery list, recipe form, ingredient search, and import sits in a sticky bar above the bottom navigation.
- *Native `<dialog>` for confirmations.* The browser traps focus, closes it on Escape, and makes the page behind it inert, which hand-built Bootstrap modals didn't do. Less-used actions (edit, delete, merge) are in a "⋯" menu instead of rows of icon buttons.
- *Plain HTTP in mind.* The Clipboard and Web Share APIs only work over HTTPS, and the app usually runs on plain HTTP at home. Copy falls back to the older select-and-copy method, and Share only appears where it works.
- *No service worker.* The manifest gives the app a home-screen icon, but there is no offline mode, because Blazor Server can't do anything without its connection to the server.
- *Bootstrap only, no component library.* The few shared pieces (dialog, menu, filter panel, stepper) are small Razor components with tests, which keeps the dependencies to two vendored files.

**Tags as a comma-separated column.** This keeps the schema small. Tag filtering is done in memory after the database applies the other filters, which is fine for a personal collection. A large catalog would use a separate tags table.

## Security

- **Uploads validated by content:** images are checked against the file signatures of an allow-list of formats (JPEG, PNG, GIF, WebP), never by file name or the content type the browser reports. SVG is excluded because it can contain scripts. The image endpoint re-checks the bytes on every request.
- **Server-side validation:** every save path goes through `RecipeValidator`, which enforces field lengths, quantity ranges, and known units. SQLite doesn't enforce column lengths itself.
- **Bounded import parsing:** imports are limited to 1 MB and ingredient lines to 500 characters, and every parser regex has a timeout. This closes a measured regular-expression denial-of-service risk.
- **Errors and logging:** users see friendly messages, not exception details. Unexpected errors are logged with structured logging (`ILogger`), and logs record IDs and counts, never recipe content. In production, unhandled errors show a generic page with a reference ID.
- **Response headers:** `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, and `Referrer-Policy: no-referrer`.
- **Data protection keys:** stored in `data/keys` next to the database, readable only by the container's user. They encrypt drafts and Blazor's circuit IDs. On Linux they aren't encrypted at rest, so protect the data folder the same way as the database.
- **Container:** multi-stage build, a chiseled runtime image with no shell or package manager, a non-root user, and app files that the app user can read but not modify.
- **Supply chain:** GitHub Actions are pinned to commit SHAs with least-privilege permissions, CodeQL scans the code and workflows, and Dependabot keeps NuGet packages, actions, and base images up to date. Bootstrap and Bootstrap Icons are served from `wwwroot/lib` rather than a CDN, so pages don't load third-party code or need internet access. Dependabot doesn't track these files, so they're updated by hand (taken from the npm package and checked against the registry's integrity hash).

## Known limitations

- **No authentication.** Anyone who can reach the app can view, change, or delete every recipe. It's designed for a single household on a trusted home network. Don't expose it to the internet without putting authentication in front of it (for example a reverse proxy with login).
- **Single-user design.** There are no user accounts or per-user data, and simultaneous edits to the same recipe aren't detected (the last save wins).
- **No HTTPS inside the container.** The container serves plain HTTP on port 8080. Use a reverse proxy such as Caddy, nginx, or Traefik for HTTPS. Over plain HTTP the grocery list's Share button is hidden, and Android may open the home-screen icon in a normal browser tab rather than as an app.
- **Drafts belong to one browser on one device.** They aren't synced, so a recipe started on a phone can't be finished on a laptop until it's saved.
- **Photos aren't kept in drafts.** They can be up to 10 MB, too much for browser storage, so a photo has to be chosen again after a draft is restored. The notice says so.
- **The last few keystrokes can be lost** if a phone suspends the page before the field being typed in has sent its value. Everything up to the previous field is kept.
- **Large pasted imports may fail.** Blazor limits messages from the browser to 32 KB, so pasting more text than that into the import box is likely to disconnect the page. Use the file upload for large imports (up to 1 MB).
- **Recipes saved by older versions show base units.** Their amounts were converted to grams, milliliters, or pieces when saved, so "2 tbsp" from back then still reads "29.57 ml". Editing and saving keeps that amount; re-entering it as "2 tbsp" stores it that way from then on (see [Design decisions](#design-decisions)).
- **No Content Security Policy yet.** All scripts are now static files (the import page's inline script was moved), so the remaining work is choosing a policy and testing that Blazor Server's script and connection work under it.

## Import format

Recipes are imported and exported as plain text. A file can hold any number of recipes, each ending with `===== END RECIPE =====`:

```text
===== RECIPE =====
Name: Pancakes
Cooking Time: 20 minutes
Servings: 4
Tags: Breakfast, Sweet

===== INGREDIENTS =====
250g flour
2 cups milk
2 eggs (beaten)
1/2 tsp salt
butter (for the pan)

===== INSTRUCTIONS =====
Whisk everything together.
Cook on a hot buttered pan.

===== NOTES =====
Best served warm.

===== END RECIPE =====
```

- **Required:** `Name`, `Cooking Time` (in minutes), `Servings`, and the instructions section. Tags, notes, and dates are optional.
- **Ingredient lines:** quantity, optional unit, name, and an optional preparation note in parentheses. Fractions (`1/2`, `1 1/4`) are supported. A line without a quantity, like `butter (for the pan)`, is kept without one.
- **Units:** g, kg, oz, lb, ml, l, cup, tbsp, tsp, fl oz, and pieces, including plurals and long forms (`grams`, `tablespoons`). Lines without a recognized unit are counted in pieces.
- A recipe with errors is reported and skipped; the other recipes in the file are still imported. The import page has the full format guide.
