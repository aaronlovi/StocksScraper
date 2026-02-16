# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build, Test & Run

```bash
# Build (warnings are errors)
dotnet build dotnet/EDGARScraper.sln

# Run all tests
dotnet test dotnet/EDGARScraper.sln

# Run a single test class
dotnet test dotnet/EDGARScraper.sln --filter "FullyQualifiedName~TaxonomyImportTests"

# Run a single test method
dotnet test dotnet/EDGARScraper.sln --filter "FullyQualifiedName~TaxonomyImportTests.MethodName"

# Run the scraper/ETL console app (example: print a financial statement)
dotnet run --project dotnet/Stocks.EDGARScraper -- --print-statement --cik 320193 --concept StatementOfFinancialPositionAbstract --date 2025-03-29 --format html

# Start the gRPC data service
dotnet run --project dotnet/Stocks.DataService

# Start local infrastructure (Postgres, pgAdmin, Elasticsearch, Kibana)
docker compose -f docker-scripts/docker-compose.yml up

# Interactive build/run menu
bash dotnet/run.sh
```

## Project Sources of Truth

- `dotnet/project_fact_sheet.md` — project purpose, user stories, architecture, tech stack, cross-cutting rules
- `dotnet/project_instructions.md` — workflow guidelines and coding standards
- `AGENTS.md` — agent-specific instructions for this repository

Always consult these before starting work. After completing work, check requirements/Kanban lists and ask the project owner before updating statuses.

## Architecture

The system is a .NET 8 backend for collecting, processing, and serving US stock market data from SEC EDGAR.

### Projects (in `dotnet/`)

- **Stocks.EDGARScraper** — Console app and main entry point. Orchestrates ETL workflows (download, parse, import) via CLI flags. `Program.cs` wires up all commands and services.
- **Stocks.Persistence** — Data access layer for PostgreSQL. Uses a **Statement pattern**: each DB operation is a separate class (e.g., `BulkInsertCompaniesStmt`, `GetTaxonomyConceptsStmt`) executed through `PostgresExecutor` with retry logic, connection pooling, and transaction support. Schema migrations run automatically on startup via Evolve (SQL scripts embedded in the project).
- **Stocks.DataService** — gRPC server exposing company/financial data to consumers.
- **Stocks.DataModels** — Shared DTOs and models (Company, Submission, DataPoint, PriceRow, taxonomy models, EDGAR file models).
- **Stocks.Shared** — Cross-cutting utilities: Result pattern (`Results.cs`), error codes, ZIP reader, semaphore guard, JSON converters, logging/metrics helpers, constants.
- **Stocks.EDGARScraper.Tests** — xUnit test project. Uses `DbmInMemoryService`/`DbmInMemoryData` for test isolation.

### Key Patterns

- **Result pattern** — Use `Result<T>` / `Result` with `ErrorCodes` enum instead of exceptions. Chain with `.Then()`, `.OnSuccess()`, `.OnFailure()`, `.OnCompletion()`. Only throw for truly unrecoverable errors.
- **Statement pattern** — Each database operation is encapsulated in its own class under `Database/Statements/`. The class receives parameters, builds the SQL, and executes via `PostgresExecutor`.
- **In-memory test doubles** — `DbmInMemoryService` implements `IDbmService` for testing without a database. `DbmInMemoryData` holds the backing data.
- **Options classes** — CLI command options are modeled as classes under `Options/` and bound in `Program.cs`.

## Coding Conventions

- **ImplicitUsings disabled** — all `using` statements must be explicit
- **Nullable enabled** — null-safety is enforced
- **TreatWarningsAsErrors** — code must compile warning-free
- **No LINQ in production code** — use explicit loops for clarity, debuggability, and allocation control. LINQ is acceptable in test code.
- **No tuples** — use record types or classes for returning/passing multiple values
- **Dictionary iteration** — use semantically meaningful deconstruction names, not `kvp`
- **Testing** — xUnit with descriptive method names. Gherkin-style SpecFlow scenarios for behavior-driven tests. Tests are required for new features unless explicitly waived.

## Workflow Requirements

- New features require a requirements document (template in `requirements.1.md` / `requirements.2.md`) with: requirements table, Kanban tasks (max 2h each), high-level design, technical design, implementation context, glossary.
- At project end, produce an ADR using MADR template in `dotnet/decisions/`.
