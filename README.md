# Recipe 0.1

A local, single-user recipe prototype built with ASP.NET Core/.NET 10, EF Core, PostgreSQL 18, React and TypeScript. The UI is Swedish. It supports recipe search and editing, canonical ingredient administration, quantity ranges, grouped ingredients, ordered steps and locally stored images.

## Architecture

The four backend projects separate domain, application, infrastructure and HTTP concerns. Dependencies point inward: Application references Domain; Infrastructure references Application; API composes both. Domain has no EF or HTTP dependencies. Focused persistence ports support recipe use cases and catalogue operations; there is no generic repository, Mediator/CQRS framework or event bus.

API contracts are explicit records, not serialized EF entities. The React app calls `/api` through Vite's local proxy. PostgreSQL is the only Docker service. Files are stored through `IImageStorage`; database rows store references and metadata only.

```text
Recipe.sln
src/
  Recipe.Domain/          Entities, Quantity/Yield and invariants
  Recipe.Application/     Contracts, use cases and focused ports
  Recipe.Infrastructure/  EF mappings, migrations, seeds, queries and disk storage
  Recipe.Api/             HTTP endpoints and local identity
  Recipe.Web/             React/TypeScript UI and frontend tests
tests/
  Recipe.Domain.Tests/
  Recipe.Application.Tests/
  Recipe.IntegrationTests/ Real PostgreSQL + in-process HTTP server
docs/
  SPECIFICATION.md
  DOMAIN_MODEL.md
compose.yaml
```

## Prerequisites

- .NET 10 SDK (`global.json` permits installed .NET 10 feature bands).
- Visual Studio with .NET 10 support and the ASP.NET/web workload, or the .NET CLI.
- Node.js 24 LTS and npm (verified with Node 24.12).
- Docker Desktop with the Linux container engine running and Docker Compose.
- Microsoft Edge for the optional browser tests; see the Playwright instructions below for alternatives.

This version always uses the seeded local administrator and has no login. It is deliberately limited to the Development/Testing environments and local use. It is not ready for public hosting.

## First run

Run these commands from the repository root:

```powershell
docker compose up -d --wait
dotnet restore Recipe.sln
dotnet tool restore
dotnet ef database update --project src/Recipe.Infrastructure --startup-project src/Recipe.Api
dotnet run --project src/Recipe.Api -- --seed
```

The development PostgreSQL connection uses `localhost:54329`, database/user `recipe` and password `recipe_local`. This is a local development credential. Compose binds the database port to loopback and persists data in the `recipe-postgres` volume. Override the API connection with `ConnectionStrings__Recipe` when needed.

Seeds run only when the database has no users. They are atomic and do not overwrite existing recipes or catalogue edits. They include Swedish/English names, aliases, units, tags and four example recipes. Edit `DevelopmentSeed.cs` to replace the examples for a fresh development database. Never remove an existing volume if its recipes or catalogue changes must be retained.

Start the backend:

```powershell
dotnet run --project src/Recipe.Api
```

In another terminal, start the frontend:

```powershell
cd src/Recipe.Web
npm ci
npm run dev
```

Open <http://127.0.0.1:5173>. The backend listens at <http://localhost:5050>; `/api/health` checks HTTP availability, not database readiness.

In Visual Studio, open `Recipe.sln`, select `Recipe.Api` as the startup project and use its `Recipe.Api` launch profile. Run the frontend command in Visual Studio's terminal. Database migration and seeding remain explicit commands.

## Editing recipes

- Create a recipe with a name, type and positive yield. Drafts may have no ingredients or steps.
- Choose ingredients from autocomplete results; names and aliases are searched. Unknown text never creates an ingredient.
- Enter an exact amount or a lower/upper range. For unspecified amounts, leave both numeric fields and the unit empty and optionally enter `efter smak` or similar wording.
- Groups have optional headings. The detail view hides unnamed headings. Entries and steps can be reordered; entries can also move between groups.
- Select tags and save explicitly. Navigation warns about unsaved edits.
- After the first save, reopen editing to upload images. Image uploads/removal and metadata are saved separately. Images support captions, ordering and a primary selection.
- Use **Ingredienskatalog** to add missing ingredients and maintain preferred translated names and aliases. Recipe types, units and tags are seed-managed in 0.1.

Search uses **all selected ingredients**, **any selected tag**, and **AND between categories**, including name and type. This is an intentional initial UX decision, to revisit with real recipes.

## Migrations

```powershell
dotnet ef migrations add DescribeChange --project src/Recipe.Infrastructure --startup-project src/Recipe.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/Recipe.Infrastructure --startup-project src/Recipe.Api
```

Migrations are not applied automatically on normal application startup. Schema changes may be substantial in later prototype iterations.

## Builds and automated tests

```powershell
dotnet build Recipe.sln
dotnet test Recipe.sln
cd src/Recipe.Web
npm ci
npm run build
npm test
```

Backend integration tests require PostgreSQL to be running (`docker compose up -d --wait`). They create a uniquely named `recipe_test_*` database, apply the real migrations, seed it and drop only that database after the tests. The configured role needs permission to create databases. An unavailable PostgreSQL server causes test failure, not a silent skip. To use a different server:

```powershell
$env:RECIPE_TEST_CONNECTION = 'Host=localhost;Port=54329;Database=postgres;Username=recipe;Password=recipe_local'
dotnet test tests/Recipe.IntegrationTests
```

For browser tests, first start and seed the backend, then run:

```powershell
cd src/Recipe.Web
npm run test:e2e
```

Playwright starts Vite if needed and uses installed Microsoft Edge in headless mode. To use bundled Chromium instead, remove `channel: 'msedge'` from `playwright.config.ts` and run `npx playwright install chromium`. Browser tests create uniquely named example recipes and catalogue entries in the running development database; use a disposable development database if those examples are unwanted.

Tests cover quantity/yield validation, catalogue authorization, unknown references, canonical aliases, PostgreSQL filtering (ingredient AND / tag OR), combined filters, create/edit and stable child IDs, cross-group moves, removal, migrations/constraints, image lifecycle, and the recipe editor.

## Local image files

Files live under `src/Recipe.Api/App_Data/images` by default, excluded from Git. Override `Images:Directory` (`Images__Directory` in the environment) to change location. Uploads accept JPEG, PNG and WebP signatures, up to 10 MB; original files are retained without resizing. Metadata references opaque generated filenames. Back up both the database and this directory if you want to preserve prototype data.

## Scope and limitations

There is no registration, public publishing, recipe deletion, autosave, conversion/scaling engine, community functionality or recipe translation workflow. `Published` is a local status. Catalogue names support language records, but the UI is not fully internationalized. Changes use last-write-wins semantics; concurrent editing is not a target of this single-user version. Image files and PostgreSQL do not share a transaction; a process crash can leave an orphan file. Image signatures are checked, but no full decoding, malware scanning or image optimization is performed.

See [the domain model](docs/DOMAIN_MODEL.md) for invariants, API conventions, assumptions and questions for evaluation.

## Implementation verification

Verified on 2026-09-25:

- Backend solution build: zero warnings and errors.
- 24 backend tests passed (9 domain, 5 application, 10 PostgreSQL/API integration).
- Frontend production build and 3 component tests passed.
- 3 browser tests passed, including recipe entry/editing, catalogue maintenance, images, unsaved changes and a narrow viewport.
- Both migrations applied to a fresh database; the Unicode search migration also rolled back and reapplied successfully.
- Docker Compose configuration validated, but this machine's Docker Desktop engine could not start because of an inaccessible `dockerInference` runtime socket. Database and browser verification therefore used temporary official PostgreSQL 18.6 Windows binaries on loopback, with the same migrations and application connection settings. The Docker-backed startup itself remains unverified on this machine. No application database-provider substitution was made.
