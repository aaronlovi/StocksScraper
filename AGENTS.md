# Repository Guidelines

## Project Structure & Module Organization

- `dotnet/` contains the .NET 8 solution (`EDGARScraper.sln`) and all C# projects.
- `dotnet/Stocks.EDGARScraper` is the core scraper/ETL console app and gRPC host.
- `dotnet/Stocks.DataService` hosts the gRPC data API service.
- `dotnet/Stocks.Persistence` and `dotnet/Stocks.DataModels` hold data access and shared DTOs/models.
- `dotnet/Stocks.Shared` contains shared utilities, logging, and cross-cutting helpers.
- Tests live in `dotnet/Stocks.EDGARScraper.Tests`.
- ADRs live in `dotnet/decisions/` with an index in `dotnet/decisions/README.md`.
- Docker assets are under `docker-scripts/` (compose files for database/services).

## Build, Test, and Development Commands

- `dotnet build dotnet/EDGARScraper.sln` compiles the full solution (warnings are errors).
- `dotnet test dotnet/EDGARScraper.sln` runs xUnit tests.
- `dotnet run --project dotnet/Stocks.EDGARScraper -- --run-all` runs the scraper workflow.
- `dotnet run --project dotnet/Stocks.DataService` starts the gRPC data service.
- `bash dotnet/run.sh` offers a simple build menu wrapper.
- `docker compose -f docker-scripts/docker-compose.yml up` starts local infra (Postgres/Redis).

## Coding Style & Naming Conventions

- C#/.NET 8 conventions: PascalCase for types/methods, camelCase for locals.
- `Nullable` is enabled and `ImplicitUsings` is disabled; include explicit `using` statements.
- Treat warnings as errors; keep analyzer warnings clean.
- Prefer `Result`/`IResult` patterns from `dotnet/Stocks.Shared/Results.cs` over exceptions.
- Avoid LINQ in production paths unless clearly justified.
- Do not use LINQ outside of unit test code.

## Testing Guidelines

- Primary framework is xUnit; SpecFlow-style Gherkin scenarios are expected for new features.
- Name tests in `*Tests.cs` files with descriptive method names (e.g., `PrintStatement_InvalidConcept_ReturnsError`).
- Run all tests with `dotnet test dotnet/EDGARScraper.sln`.

## Commit & Pull Request Guidelines

- Recent commits use sentence-case summaries; feature work often follows `Area - detail` (e.g., `Prototype Financial Statement Viewer - Add ...`).
- Keep commits focused and descriptive; include rationale in the PR description.
- PRs should link relevant requirements/ADR updates and mention tests run.

## Agent-Specific Instructions

- Treat `dotnet/project_fact_sheet.md` and `dotnet/project_instructions.md` as the source of truth.
- When iterating through dictionaries, use semantically meaningful names for key/value pairs (prefer deconstruction with descriptive names over generic `kvp`).
- For new features, create or update requirements docs and ADRs under `dotnet/decisions/`.
- After finishing work, check requirements/Kanban lists and ask before updating statuses.
