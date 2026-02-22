# Research: Investment Return Report for Graham/Buffett Score Pages

## Objective
Understand what data, APIs, and UI components exist (and what's missing) to add "If I invested $1000 on day X, how much would I have today?" reporting to the Graham and Buffett score pages — both list pages (aggregate) and detail pages (per-company).

## Context
- Guidelines: `CLAUDE.md`, `dotnet/project_fact_sheet.md`, `dotnet/project_instructions.md`
- Frontend: Angular standalone components with signals, located in `frontend/stocks-frontend/src/app/`
- Backend: .NET 8 minimal API + PostgreSQL, located in `dotnet/`
- Scoring list pages:
  - Graham: `frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.ts` — API: `GET /api/reports/scores`
  - Buffett: `frontend/stocks-frontend/src/app/features/moat-scores-report/moat-scores-report.component.ts` — API: `GET /api/reports/moat-scores`
- Scoring detail pages:
  - Graham: `frontend/stocks-frontend/src/app/features/scoring/scoring.component.ts` — API: `GET /api/companies/{cik}/scoring`
  - Buffett: `frontend/stocks-frontend/src/app/features/moat-scoring/moat-scoring.component.ts` — API: `GET /api/companies/{cik}/moat-scoring`
- Pre-computed score summary tables: `company_scores` (Graham), `company_moat_scores` (Buffett) — both have `computed_at` timestamp but no historical score snapshots
- Price data: `prices` table with OHLC + volume, keyed by `(ticker, price_date)`. Accessed via `GetPricesByTickerStmt` (all history) and `GetAllLatestPricesStmt` (latest per ticker)
- Price model: `PriceRow` record in `dotnet/Stocks.DataModels/PriceRow.cs`
- Report endpoints: `dotnet/Stocks.WebApi/Endpoints/ReportEndpoints.cs`, `MoatReportEndpoints.cs`
- API service: `frontend/stocks-frontend/src/app/core/services/api.service.ts`

## Questions to Answer

1. **What does "day X" mean operationally?** The `company_scores` and `company_moat_scores` tables store only the latest computation (`computed_at`). Is there any historical record of when a company first achieved a given score threshold (e.g., 13/13 Graham)? If not, what is the earliest price date available in the `prices` table for a typical scored company, and could `computed_at` serve as a proxy? Examine the `prices` table schema, any price import tracking tables, and the `company_scores`/`company_moat_scores` DDL for historical columns.

2. **What price history depth exists?** How far back does price data go for a typical company? Examine `BulkInsertPricesStmt`, price import logic, and any Stooq download code to determine the date range of imported prices. This affects whether "day X" can be the score's `computed_at` date or must be something else.

3. **How are list-page results filtered by score?** The list endpoints accept `minScore` and `maxScore` query params. Examine `dotnet/Stocks.Persistence/Database/Statements/GetCompanyScoresStmt.cs` and `GetCompanyMoatScoresStmt.cs` to understand how the filtering works, what columns are returned, and whether the query could be extended to include a per-company return calculation or if it should be a separate query/enrichment step.

4. **What backend response DTOs exist for the list pages and detail pages?** Examine `CompanyScoreSummary`, `CompanyMoatScoreSummary`, `ScoringResponse`, `MoatScoringResponse` and their related DTOs to understand what fields are already returned and what new fields would be needed for investment return data.

5. **How does the detail page currently link from the list page?** Examine the Angular routing and templates to understand the navigation from list → detail, and where a "view investment return" link or section could be added on the detail pages.

6. **Is there any existing return-on-investment or performance calculation logic anywhere in the codebase?** Search for terms like `return`, `investment`, `performance`, `cagr`, `annualized` in both backend and frontend code to find any existing patterns to reuse.

7. **What is the formula for annualized return?** Confirm the standard formula: `AnnualizedReturn = ((EndValue / StartValue) ^ (365.25 / DaysHeld)) - 1`. Check if any existing code already implements this (e.g., `revenue_cagr` in moat scoring).

8. **How does the batch score computation pipeline work?** Examine the `--compute-all-scores` and `--compute-all-moat-scores` CLI commands in `Program.cs` and the associated services to understand how pre-computed scores are generated. Determine whether the investment return calculation should be added to this batch pipeline (pre-computed) or computed on-the-fly per request.

## Explore
- `dotnet/Stocks.Persistence/Database/Migrations/V013__AddCompanyScoresTable.sql` — Graham scores DDL
- `dotnet/Stocks.Persistence/Database/Migrations/V017__AddCompanyMoatScoresTable.sql` — Buffett scores DDL
- `dotnet/Stocks.Persistence/Database/Statements/GetPricesByTickerStmt.cs` — price history query
- `dotnet/Stocks.Persistence/Database/Statements/GetAllLatestPricesStmt.cs` — latest price query
- `dotnet/Stocks.Persistence/Database/Statements/GetCompanyScoresStmt.cs` — Graham scores query
- `dotnet/Stocks.Persistence/Database/Statements/GetCompanyMoatScoresStmt.cs` — Buffett scores query
- `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyScoresStmt.cs` — Graham batch insert
- `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyMoatScoresStmt.cs` — Buffett batch insert
- `dotnet/Stocks.WebApi/Endpoints/ReportEndpoints.cs` — Graham scores list endpoint
- `dotnet/Stocks.WebApi/Endpoints/MoatReportEndpoints.cs` — Buffett scores list endpoint
- `dotnet/Stocks.WebApi/Endpoints/ScoringEndpoints.cs` — Graham detail endpoint
- `dotnet/Stocks.WebApi/Endpoints/MoatScoringEndpoints.cs` — Buffett detail endpoint
- `dotnet/Stocks.DataModels/Scoring/CompanyScoreSummary.cs` — Graham list DTO
- `dotnet/Stocks.DataModels/Scoring/CompanyMoatScoreSummary.cs` — Buffett list DTO
- `dotnet/Stocks.DataModels/Scoring/` — all scoring DTOs
- `dotnet/Stocks.EDGARScraper/Program.cs` — CLI command wiring, batch score commands
- `dotnet/Stocks.EDGARScraper/Services/ScoringService.cs` or similar — score computation
- `dotnet/Stocks.EDGARScraper/Services/MoatScoringService.cs` or similar — moat score computation
- `frontend/stocks-frontend/src/app/features/scores-report/` — Graham list page (template + component)
- `frontend/stocks-frontend/src/app/features/moat-scores-report/` — Buffett list page (template + component)
- `frontend/stocks-frontend/src/app/features/scoring/` — Graham detail page
- `frontend/stocks-frontend/src/app/features/moat-scoring/` — Buffett detail page
- `frontend/stocks-frontend/src/app/core/services/api.service.ts` — API client
- `frontend/stocks-frontend/src/app/app.routes.ts` — routing
- Search codebase for `cagr`, `annualized`, `return`, `investment`, `performance` patterns
- `dotnet/Stocks.Persistence/Database/Migrations/` — look for any price import tracking migration
- Stooq price download code (search for `stooq` or `PriceImport` in `dotnet/`)

## Output
Write findings to `.prompts/investment-return-report-research/research.md`:
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

<!-- Self-review: converged after 2 passes -->
