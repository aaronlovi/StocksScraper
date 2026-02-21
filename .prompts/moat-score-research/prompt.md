# Research: Moat Score — Alternate Scoring System with Frontend

## Objective

Understand everything needed to implement the Moat Score as a second, independent scoring system alongside the existing Value Score. This covers backend scoring logic, new XBRL concept imports, database schema, API endpoints, and full frontend presentation (list page, detail page with scorecard, trend charts with sparklines, and sidebar navigation).

## Context

- Guidelines: `CLAUDE.md`, `dotnet/project_fact_sheet.md`, `dotnet/project_instructions.md`
- Design spec: `docs/moat-score-design.md` — 13 checks, 6 trend charts, data requirements
- Existing scoring logic: `dotnet/Stocks.Persistence/Services/ScoringService.cs`
- Scoring models: `dotnet/Stocks.DataModels/Scoring/` (DerivedMetrics, ScoringCheck, ScoringResult, CompanyScoreSummary)
- Scoring endpoints: `dotnet/Stocks.WebApi/Endpoints/ScoringEndpoints.cs`
- Reports endpoint: `dotnet/Stocks.WebApi/Endpoints/ReportEndpoints.cs`
- Pre-computed scores table: `dotnet/Stocks.Persistence/Database/Migrations/V013__AddCompanyScoresTable.sql`
- Bulk insert: `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyScoresStmt.cs`
- Data point queries: `dotnet/Stocks.Persistence/Database/Statements/GetScoringDataPointsStmt.cs`, `GetAllScoringDataPointsStmt.cs`
- CLI entry point: `dotnet/Stocks.EDGARScraper/Program.cs` (see `--compute-all-scores`)
- Frontend framework: Angular 21 (standalone components, signals, RxJS)
- Frontend scoring detail: `frontend/stocks-frontend/src/app/features/scoring/scoring.component.ts`
- Frontend scores list: `frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.ts`
- Frontend API service: `frontend/stocks-frontend/src/app/core/services/api.service.ts`
- Frontend sidebar: `frontend/stocks-frontend/src/app/core/layout/sidebar/sidebar.component.ts`
- Frontend routing: `frontend/stocks-frontend/src/app/app.routes.ts`
- Stack: .NET 8 (C#), PostgreSQL, Angular 21, TypeScript

## Questions to Answer

### Backend — Scoring Logic

1. **How is `ScoringService.ComputeScore()` structured?** Read the full method. Document the flow: concept resolution → data fetching → grouping by year → derived metrics computation → check evaluation → result assembly. Which parts can be reused vs. must be written fresh for Moat checks?

2. **How are derived metrics computed?** Read `DerivedMetrics.cs` and the metric computation code in `ScoringService`. For each metric the Moat Score shares with the Value Score (ROE CF, ROE OE, owner earnings, CapEx, dividends paid, debt-to-equity, estimated returns), document how it's computed and whether it can be called from Moat scoring without duplication.

3. **How are scoring checks structured?** Read `ScoringCheck.cs` (or equivalent). How does the check evaluation work — is it a list of check objects with thresholds, or hardcoded if/else? How are results stored (pass/fail/na)? Can the same structure hold Moat checks with different thresholds, or does it need extension?

4. **How does concept resolution work?** The Value Score defines ~144 concepts with fallback chains. Document the pattern: how are fallback arrays defined, how does resolution pick the first available concept, and how would new concepts (Revenue fallbacks, COGS fallbacks, GrossProfit, OperatingIncomeLoss, InterestExpense/InterestExpenseDebt) be added?

5. **What concepts are already resolved by the Value Score that the Moat Score also needs?** Cross-reference the Moat Score's 13 checks with the Value Score's existing concept resolution. Identify exactly which concepts are already imported and available vs. which are new (per `docs/moat-score-design.md` section 5.2).

### Backend — New Metrics

6. **How should gross margin, operating margin, revenue CAGR, capex ratio, interest coverage, and "positive OE every year" be computed?** For each new metric, determine: which concepts are needed, which already exist in the data, what the computation formula is (per the design doc), and where in the code flow it should be computed. Pay attention to the averaging pattern described in the design doc (per-year values averaged across years, same as Value Score).

7. **How should "consistent dividend or buyback" be evaluated?** The check requires capital returned in >= 75% of years. Determine: do we already have dividends paid and stock repurchase data per year? How is "returned capital" defined — dividends + buybacks, or just dividends? Read how dividends and stock repurchase are currently handled in the Value Score.

### Backend — Data Layer

8. **How does `GetScoringDataPointsStmt` fetch data, and what changes for Moat Score?** Read the SQL query. Does it fetch by a hardcoded list of concept names, or by a parameter? How would we add the new concepts (Revenue, COGS, GrossProfit, OperatingIncomeLoss, InterestExpense) to the fetch? Does `GetAllScoringDataPointsStmt` work the same way?

9. **What's the current `company_scores` table schema?** Read migration V013. Document all columns. Determine whether the Moat Score should get its own `company_moat_scores` table (preferred for separation) or extend the existing table. What columns would the moat summary table need?

10. **How does bulk score computation work end-to-end?** Trace the `--compute-all-scores` CLI command from `Program.cs` through to database insertion. Document the flow: truncate → fetch all data → compute per company → bulk insert. How would a `--compute-all-moat-scores` command be structured?

### Backend — API

11. **How are scoring endpoints structured?** Read `ScoringEndpoints.cs` and `ReportEndpoints.cs`. Document the request/response shapes. For the Moat Score, we need: (a) on-demand single-company moat score computation endpoint, (b) pre-computed moat scores list/report endpoint with pagination/sorting/filtering, (c) trend data endpoint for the 6 charts. What's the cleanest way to add these without modifying existing Value Score endpoints?

### Frontend — List Page

12. **How does `ScoresReportComponent` work?** Read the full component. Document: data fetching, table rendering, column definitions, sorting, filtering, pagination, color coding, row highlighting. What columns would the Moat Scores list page need (score, company, ticker, exchange, price, gross margin, operating margin, avg ROE, est. return, revenue CAGR)?

### Frontend — Detail Page

13. **How does `ScoringComponent` work?** Read the full component. Document: sections rendered (header, score badge, scorecard table, derived metrics, AR/Revenue trend, raw data), data flow, signal usage. The Moat detail page needs the same structure but with Moat checks and 6 trend charts instead of 1.

14. **How is the AR/Revenue sparkline implemented?** Read the sparkline computed signal in `ScoringComponent`. Document: SVG generation, data mapping, axis scaling, point rendering, responsive sizing. This pattern will be reused for 5 additional trend charts (gross margin, operating margin, ROE CF, ROE OE, revenue).

### Frontend — Navigation & Routing

15. **How are routes and sidebar entries defined?** Read `app.routes.ts` and `sidebar.component.ts`. What's the pattern for adding a new top-level section? The Moat Score needs: `/moat-scores` (list page) and `/company/:cik/moat-scoring` (detail page), plus a sidebar entry with tooltip.

### Frontend — API Service

16. **What TypeScript interfaces exist for scoring?** Read `api.service.ts` focusing on `ScoringResponse`, `CompanyScoreSummary`, `ScoringCheckResponse`, `DerivedMetricsResponse`, `ArRevenueRow`. Which can be reused for Moat Score, and which need moat-specific equivalents?

### Testing

17. **How are scoring tests structured?** Find and read the existing scoring test files. What patterns do they use (in-memory test doubles, xUnit conventions)? What would Moat Score tests need to cover?

### Cross-Cutting

18. **Are there any shared abstractions that could serve both scoring systems?** Look for interfaces like `IScoringService`, base classes, or generic patterns that could be extended. Or is the existing code monolithic (single `ScoringService` class) requiring a separate `MoatScoringService`?

## Explore

- `dotnet/Stocks.Persistence/Services/ScoringService.cs` — full file, all methods
- `dotnet/Stocks.DataModels/Scoring/` — all files in directory
- `dotnet/Stocks.Persistence/Database/Statements/GetScoringDataPointsStmt.cs`
- `dotnet/Stocks.Persistence/Database/Statements/GetAllScoringDataPointsStmt.cs`
- `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyScoresStmt.cs`
- `dotnet/Stocks.Persistence/Database/Migrations/V013__AddCompanyScoresTable.sql`
- `dotnet/Stocks.WebApi/Endpoints/ScoringEndpoints.cs`
- `dotnet/Stocks.WebApi/Endpoints/ReportEndpoints.cs`
- `dotnet/Stocks.WebApi/Endpoints/CompanyEndpoints.cs` (AR/Revenue endpoint)
- `dotnet/Stocks.EDGARScraper/Program.cs` — scoring-related CLI commands
- `frontend/stocks-frontend/src/app/features/scoring/scoring.component.ts`
- `frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.ts`
- `frontend/stocks-frontend/src/app/core/services/api.service.ts`
- `frontend/stocks-frontend/src/app/core/layout/sidebar/sidebar.component.ts`
- `frontend/stocks-frontend/src/app/app.routes.ts`
- `dotnet/Stocks.EDGARScraper.Tests/Scoring/ScoringServiceTests.cs`
- `dotnet/Stocks.EDGARScraper.Tests/Scoring/ScoringModelTests.cs`
- `dotnet/Stocks.EDGARScraper.Tests/Scoring/BatchScoringServiceTests.cs`
- `dotnet/Stocks.EDGARScraper.Tests/Scoring/GetScoringDataPointsTests.cs`
- `dotnet/Stocks.Persistence/Services/` — check for interfaces or base classes
- `docs/moat-score-design.md` — the design specification

## Output

Write findings to `.prompts/moat-score-research/research.md`:
- Answers to all 18 questions above, organized by section
- Existing patterns to follow (concept resolution, check evaluation, sparkline rendering, endpoint structure)
- New concepts and metrics that must be added
- Risks or concerns (e.g., XBRL concept availability, performance of additional queries, frontend complexity)
- Recommended approach for keeping Moat Score fully independent from Value Score while maximizing code reuse
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
