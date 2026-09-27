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

## Activity modes

- **Hitta & laga** is the default view: search, read and print locally published recipes. Drafts do not appear here. A discreet edit shortcut on your own recipes opens creator mode.
- **Skapa & utforska** contains **Mina recept** (your own drafts and published recipes), a status filter and **+ Nytt recept**. Its **Ingredienser** page lets you search or create an ingredient without starting a recipe. Inline ingredient creation remains available in the editor.
- **Administration** groups the existing ingredient, Tag and RecipeType maintenance screens and is visible only to administrators. APIs enforce catalogue permissions independently of the UI.

Recipe discovery defaults to `GET /api/recipes?scope=Published`; the creator list uses `scope=Mine` with the server's current user and optional `publicationState=Draft|Published`. Draft detail/image access is owner-only. No authentication or user switching has been added; the local seeded administrator is still used.

Approved product decisions, implemented behavior and explicitly deferred capabilities are distinguished in [PRODUCT_DECISIONS.md](docs/PRODUCT_DECISIONS.md).

## Editing recipes

- Save an incomplete draft even without a name, recipe type, yield, ingredients or steps. Unnamed drafts display **Namnlöst recept**. Published recipes require a name, type, valid yield, ingredient and step. Entered quantities/references must always remain structurally valid.
- Recipes containing **PendingReview** ingredients can be saved as drafts but cannot be published, including edits of already published recipes. The editor names the affected ingredients and offers **Spara som utkast**. The server checks current catalogue status before saving. Replace pending entries with active ones or wait for future approval support; no approval/merge UI exists yet.
- The Swedish yield label is **Ger**. A blank yield is optional for drafts; once entered it needs a valid amount/unit.
- Ingredient autocomplete starts at two characters and ranks preferred-name/alias matches. Unknown text never silently creates an ingredient. With no match, choose **+ Skapa ”…”** to enter a preferred name and language in a dialog; the new canonical ingredient is selected without leaving or saving the recipe editor.
- Enter an exact amount or a lower/upper range. For unspecified amounts, leave both numeric fields and the unit empty and optionally enter `efter smak` or similar wording.
- Groups have optional headings. The detail view hides unnamed headings. Entries and steps can be reordered; entries can also move between groups.
- Select tags and save explicitly. Navigation warns about unsaved edits.
- After the first save, reopen editing to upload images. Image uploads/removal and metadata are saved separately. Images support captions, ordering and a primary selection.
- **Ingredienskatalog** opens with all ingredients alphabetically sorted by their Swedish display names. Search names/translations/aliases and filter **Alla**, **Aktiv** or **Väntar på granskning**. Select entries to maintain preferred names and aliases. Units remain seed-managed.
- **Taggar** and **Recepttyper** provide administrator creation, translation editing and activation/inactivation without manually entered IDs or codes. Tags default to all entries in Swedish alphabetical order and can be searched by any translated name. Both offer **Alla**, **Aktiva**, **Inaktiva** filters. RecipeTypes also expose numeric sort order; lower values appear first, with alphabetical ordering for ties.
- Inactive tags/types remain visible on existing recipes and in search filters. Editors can retain or remove existing inactive references, but cannot add them to another recipe. Reactivate an entry to make it selectable again. Nothing is physically deleted.
- Tags and RecipeTypes support language-tagged aliases. Administrators decide which names denote the same concept; no synonyms are inferred. Catalogue search and the recipe editor/search controls match preferred translations and aliases, display the preferred name, and select the canonical ID.
- The current administrator creates Active ingredients immediately. The same service creates PendingReview ingredients for future ordinary authors, visible/selectable only to their creator until approval. Administrator catalogue inspection can see pending entries; approval and merging are deferred. No authentication or multi-user screens have been added.

Search uses **all selected ingredients**, **any selected tag**, and **AND between categories**, including name and type. This is an intentional initial UX decision, to revisit with real recipes.

## Printing recipes

Open a recipe and choose **Skriv ut** to print or save as PDF through the browser dialog. The compact print layout is designed for A4 portrait: recipe content and only the primary image are included; navigation, buttons and the gallery are excluded. Small groups and short steps stay together where possible, while long content can span pages. Browser paper size, scale and headers/footers affect pagination; use A4, 100% scale and disable browser headers/footers for the inspected baseline.

With the development API running, create the three clearly labeled print-test recipes without overwriting existing recipes:

```powershell
cd src/Recipe.Web
node scripts/create-print-samples.ts
```

The command is repeatable and prints links for short, two-page and long samples. These are layout fixtures, not cooking recipes. Check print preview with a real primary image, unusually long groups/steps, your preferred printer and other paper sizes. There is no server PDF generator or custom page-numbering implementation.

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

Verified on 2026-09-27:

- Backend solution build: zero warnings and errors.
- 39 backend tests passed (10 domain, 5 application, 24 PostgreSQL/API integration), including scoped recipe lists, private drafts, PendingReview publication rejection for create/update, preserved content after rejection, catalogue permissions and previous lifecycle/alias behavior.
- Frontend production build and 17 component tests passed, including activity navigation, role-gated administration, publication guidance and standalone ingredient creation.
- 9 browser tests passed, including separate discovery/creator lists, standalone ingredient creation, incomplete drafts, inline creation, unsaved edits, Tag/RecipeType aliases, images, narrow layout and print-media content/layout behavior.
- Docker-backed PostgreSQL was used. All migrations applied in isolated integration databases; browser tests used a separate disposable database.
- `DraftsAndIngredientLifecycle` was applied to the existing development database after a local backup, preserving its 5 recipes, 15 recipe ingredients and 17 canonical ingredients. All existing ingredients became Active. The database was not reset or reseeded.
- The subsequent `TagAndRecipeTypeAdministration` migration was also applied in place after backup. All 4 existing Tags and 8 RecipeTypes became active, and RecipeType sort orders start at zero. Recipe IDs and references are unchanged.
- `CatalogueAliases` was applied in place after another backup; existing catalogue entries and recipe references were preserved. Short/medium/long print fixtures were visually inspected as browser-generated A4 PDFs (1/2/5 pages); page counts are observations, not automated assertions.
- The activity-mode/publication-rule iteration requires no schema migration. It does not rewrite existing recipes or catalogue records. Favorites, ratings, comments, food pairing, authentication, public distribution and approval/merge remain future work.
