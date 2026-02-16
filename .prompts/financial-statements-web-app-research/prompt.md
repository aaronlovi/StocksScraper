# Research: Financial Statements Web Application

## Objective
Understand what infrastructure, APIs, data, and patterns exist in the StocksScraper codebase to support building an Angular front-end, a .NET Web API, and a long-running .NET backend for serving financial statements. Identify gaps, design constraints, and recommended approaches before implementation.

## Context
- Guidelines: `CLAUDE.md`
- Project overview: `dotnet/project_fact_sheet.md`, `dotnet/project_instructions.md`
- Existing gRPC service: `dotnet/Stocks.DataService/`
- Data access layer: `dotnet/Stocks.Persistence/Database/IDbmService.cs`
- Data models: `dotnet/Stocks.DataModels/`
- Proto definitions: `dotnet/Stocks.Shared/Protos/`
- Statement rendering logic: `dotnet/Stocks.EDGARScraper/Services/Statements/StatementPrinter.cs`
- Architecture decisions: `dotnet/decisions/`
- Stack: .NET 8, PostgreSQL, gRPC (existing), Angular (new), ASP.NET Web API (new)

## Questions to Answer

### Data Layer & API Surface

1. **Company search data**: What data exists for searching companies by CIK, ticker symbol, or name? Examine `companies`, `company_names`, `Instrument` records, and the SEC ticker mappings. Is there a single query that can search across all three identifiers, or do we need a new statement? What indexes exist on these columns?

2. **Submissions per company**: Can we efficiently query submissions (filings) for a specific company? Is there a `GetSubmissionsByCompanyId` method, or only `GetSubmissions()` (all)? What filing types and categories are available? Examine the `Submission` model and related DB statements.

3. **Statement rendering reuse**: `StatementPrinter` currently writes to `TextWriter` streams. Can its hierarchy traversal and data-assembly logic be extracted into a reusable service that returns structured data (not formatted text) for the Web API to serialize as JSON? What would need to change?

4. **Taxonomy year resolution**: We just added `--taxonomy-year` inference from `--date`. How should the Web API resolve the taxonomy year for a given company/filing? Should it default to the filing's report year, or offer a picker?

5. **Dashboard statistics**: What aggregate queries would be useful for a dashboard? Consider: total companies, total filings, filings by type, date range coverage, taxonomy concept counts, price data coverage. Do any of these queries already exist, or do we need new DB statements?

### Architecture & Integration

6. **Web API vs gRPC**: The existing `Stocks.DataService` uses gRPC. Should the Angular front-end talk to gRPC-Web, or should we add a separate ASP.NET Web API project with REST/JSON endpoints? Consider: Angular gRPC-Web support, browser compatibility, development complexity, and whether both can coexist in the same host.

7. **Long-running backend**: The user mentions a "long-running .NET backend." Is this the existing `Stocks.DataService` (gRPC server), or a new background service? What would it do — pre-compute statements, cache taxonomy trees, handle search indexing? Clarify the role relative to the Web API.

8. **Trie-based search**: For unified typeahead search by ticker or company name, evaluate: (a) in-memory trie loaded at startup from DB, (b) PostgreSQL `LIKE`/`ILIKE` with trigram indexes, (c) Elasticsearch (already in docker-compose). Which approach fits the existing stack and data volume?

9. **Project structure**: Where should the new projects live? Consider: `dotnet/Stocks.WebApi/` for the API, a top-level `frontend/` or `angular/` directory for the Angular app. How should the solution file be updated?

10. **Shared persistence**: The Web API will need `IDbmService`. Can it reference `Stocks.Persistence` directly, or should there be an intermediate service layer? Examine how `Stocks.DataService` currently consumes `IDbmService`.

### Front-End Design

11. **Angular routing structure**: Given the views described (search, company semi-detail, company detail/report, dashboard), what Angular routes and components are needed? Consider lazy loading, route parameters (CIK, submission ID, taxonomy root concept).

12. **Pagination support**: The existing `PaginationRequest`/`PaginationResponse` in the data layer — can these be reused for paginated search results in the Web API? What page sizes are reasonable?

13. **Report type enumeration**: For the semi-detail view showing available report types, what defines a "report type"? Is it the filing type (10-K, 10-Q), the taxonomy presentation root concept (e.g., `StatementOfFinancialPositionAbstract`), or both? How does `--list-statements` output map to this?

## Explore

### Existing Data Access
- `dotnet/Stocks.Persistence/Database/IDbmService.cs` — full interface
- `dotnet/Stocks.Persistence/Database/Statements/` — all DB statement classes
- `dotnet/Stocks.Persistence/Database/Migrations/` — schema definitions and indexes

### Existing Services
- `dotnet/Stocks.DataService/` — gRPC service setup, Startup.cs, how IDbmService is injected
- `dotnet/Stocks.EDGARScraper/Services/Statements/StatementPrinter.cs` — hierarchy traversal logic
- `dotnet/Stocks.EDGARScraper/Services/Statements/` — all statement-related models (HierarchyNode, TraverseContext)

### Data Models
- `dotnet/Stocks.DataModels/` — Company, Submission, DataPoint, Instrument, PriceRow, Pagination
- `dotnet/Stocks.Persistence/Database/DTO/` — taxonomy DTOs
- `dotnet/Stocks.Shared/Protos/` — existing proto definitions

### Search & Indexing
- `docker-scripts/docker-compose.yml` — check if Elasticsearch is configured and how
- `dotnet/Stocks.EDGARScraper/Services/SecTickerMappingsDownloader.cs` — ticker data source
- Database indexes on company tables (check migrations)

### Configuration & Infrastructure
- `dotnet/Stocks.DataService/Startup.cs` — existing service registration
- `dotnet/Stocks.EDGARScraper/Program.cs` — how the host is built, DI setup

## Output
Write findings to `.prompts/financial-statements-web-app-research/research.md`:
- Answers to the questions above
- Existing patterns to follow
- Risks or concerns
- Recommended approach

Include a metadata block at the end:
```
## Metadata
### Status
[success | partial | failed]
### Dependencies
- [files or decisions this relies on, or "None"]
### Open Questions
- [unresolved issues, or "None"]
### Assumptions
- [what was assumed, or "None"]
```
