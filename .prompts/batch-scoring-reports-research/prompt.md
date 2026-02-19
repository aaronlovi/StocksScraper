# Research: Batch Scoring for Top/Bottom/All Companies Reports

## Objective

Understand what architectural changes are needed to compute 13-point scores across all companies in the database fast enough to generate ranked reports (top/bottom/all) on the fly, or near-on-the-fly with pre-computation.

## Context

- Guidelines: `CLAUDE.md`, `dotnet/project_fact_sheet.md`, `dotnet/project_instructions.md`
- Existing scoring logic: `dotnet/Stocks.Persistence/Services/ScoringService.cs` (630 lines, per-company)
- Scoring SQL: `dotnet/Stocks.Persistence/Database/Statements/GetScoringDataPointsStmt.cs`
- Scoring endpoint: `dotnet/Stocks.WebApi/Endpoints/ScoringEndpoints.cs` (single-company only)
- Data models: `dotnet/Stocks.DataModels/Scoring/`
- DB interface: `dotnet/Stocks.Persistence/Database/IDbmService.cs`
- DB schema migrations: `dotnet/Stocks.Persistence/Database/Migrations/`
- Stack: .NET 8, PostgreSQL, gRPC, Redis (planned), Angular frontend

### The Problem

`ScoringService.ComputeScore(companyId)` performs **4 sequential DB round-trips** per company:

1. `GetScoringDataPoints` — complex query with DISTINCT ON, subqueries, joins across `data_points`, `taxonomy_concepts`, `submissions` (fetches 5 years of 10-K data for ~106 concept names)
2. `GetCompanyById` — company lookup
3. `GetCompanyTickersByCompanyId` — ticker lookup
4. `GetPricesByTicker` — all prices for the ticker, then finds max date in C#

Running this for thousands of companies is prohibitively slow. The TSX scraper project (independent, not in this repo) solves this with pre-computed scores. We need a strategy for this project.

## Questions to Answer

1. **How many companies are in the database?** Query the `companies` table count. Also check how many have 10-K submissions with data points (i.e., are scoreable). This determines the scale of the batch operation.

2. **What is the current per-company scoring query cost?** Examine `GetScoringDataPointsStmt.Sql` — it uses DISTINCT ON with a correlated subquery to find the latest 5 report dates. How expensive is this per company, and what indexes exist on `data_points`, `submissions`, `taxonomy_concepts`?

3. **What DB indexes currently exist?** Read the migration SQL files to catalog indexes on `data_points`, `submissions`, `taxonomy_concepts`, and `prices` tables. Are there composite indexes that would support a batch scoring query?

4. **Can the scoring SQL be rewritten as a single batch query across all companies?** The current query is parameterized by `@company_id`. Could a set-based approach (materialized view, or a single query joining all companies) compute raw data for all companies at once? What would the SQL look like?

5. **What pre-computation strategy fits best?** Consider:
   - **Materialized view**: PostgreSQL refreshes periodically; query is instant
   - **Scoring summary table**: A `company_scores` table populated by an ETL step or background job
   - **Redis cache**: Compute on demand, cache results, invalidate on new data
   - **Hybrid**: Pre-compute summary rows in Postgres, serve from cache
   Which approach aligns with the existing architecture (Statement pattern, Result pattern, no LINQ)?

6. **What data does the report need per company?** Look at what the TSX scraper's top/bottom report shows (if any references exist in this repo). At minimum, the ranked report likely needs: company name, CIK, ticker, overall score, computable checks count, and key metrics (book value, market cap, P/B, D/E, estimated returns). Confirm by examining `ScoringResult` and `DerivedMetrics`.

7. **What API shape would the report endpoint need?** Consider pagination, sorting (by score, by metric), filtering (min score, exchange, etc.). Look at existing pagination patterns in `SearchCompaniesStmt` and the proto files.

8. **How does price data factor in?** The current scoring fetches ALL price rows per ticker to find the latest. For batch scoring, this is N full-table scans. Is there a `latest_price` view or index? Check schema.

9. **What is the `IDbmService` interface?** Catalog its methods to understand what data access patterns already exist and what new methods would be needed.

10. **Are there any existing batch/bulk patterns in the codebase?** Look at `BulkInsert*` statements and ETL workflows in `Stocks.EDGARScraper` for patterns that process all companies at once.

## Explore

- `dotnet/Stocks.Persistence/Database/Migrations/` — all SQL migration files for schema and indexes
- `dotnet/Stocks.Persistence/Database/IDbmService.cs` — full interface
- `dotnet/Stocks.Persistence/Database/Statements/` — all statement classes, especially `SearchCompaniesStmt.cs`, `BulkInsert*`
- `dotnet/Stocks.Persistence/Database/Statements/GetPricesByTickerStmt.cs` — price query
- `dotnet/Stocks.DataModels/Scoring/ScoringResult.cs` — result shape
- `dotnet/Stocks.DataModels/Scoring/DerivedMetrics.cs` — metric fields
- `dotnet/Stocks.Shared/Protos/` — existing gRPC definitions and pagination
- `dotnet/Stocks.EDGARScraper/Program.cs` — CLI commands and ETL orchestration
- `dotnet/Stocks.WebApi/` — existing endpoint patterns
- `dotnet/project_fact_sheet.md` — architecture and user stories
- `docker-scripts/docker-compose.yml` — infrastructure (Redis availability?)

## Output

Write findings to `.prompts/batch-scoring-reports-research/research.md`:

- Answers to the questions above
- Existing patterns to follow
- Risks or concerns
- Recommended approach
- Metadata block (append at end):

  ## Metadata

  ### Status

  [success | partial | failed]

  ### Dependencies

  - [files or decisions this relies on, or "None"]

  ### Open Questions

  - [unresolved issues, or "None"]

  ### Assumptions

  - [what was assumed, or "None"]
