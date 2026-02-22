# Plan: Investment Return Report for Graham/Buffett Score Pages

## Context
- Research: `.prompts/investment-return-report-research/research.md`
- Guidelines: `CLAUDE.md`, `dotnet/project_fact_sheet.md`, `dotnet/project_instructions.md`

## Instructions
1. Read research.md thoroughly — it contains findings on existing DTOs, endpoints, price data, scoring pipeline, UI routing, CAGR patterns, and a recommended approach
2. Design implementation as checkpoints following the recommended approach order from research (detail pages first, then list pages)
3. Each checkpoint must include:
   - Build: what to implement
   - Test: what unit tests to write for THIS checkpoint's code
   - Verify: how to confirm all existing + new tests pass before moving on
4. NEVER design a dedicated "testing" checkpoint at the end. Tests are written alongside the code they verify, within the same checkpoint. Each checkpoint must leave the test suite green.

## Key Constraints from Research
- **No LINQ in production code** — use explicit loops
- **Result pattern** — return `Result<T>` with `ErrorCodes` for all new service/statement code
- **Statement pattern** — each DB query is its own class under `Database/Statements/`
- **Implicit usings disabled** — all `using` statements must be explicit
- **Nullable enabled** — null-safety enforced
- **TreatWarningsAsErrors** — code must compile warning-free
- **No tuples** — use record types or classes
- **Angular signals** — use `signal<T>()`, `computed()` for frontend state
- **xUnit tests** required for new features

## Scope Summary (from Research)
1. **Detail page endpoint** — `GET /api/companies/{cik}/investment-return?startDate=YYYY-MM-DD` returning start/end price, dates, total return %, annualized return %, $1000 invested value
2. **Detail page UI** — "Investment Return" card on both Graham `ScoringComponent` and Buffett `MoatScoringComponent` with date picker and return display
3. **List page pre-computed column** — add `return_1y` (or similar fixed-period return) to `company_scores` and `company_moat_scores` tables, computed during batch pipeline
4. **List page UI column** — sortable 1-year return column on both Graham and Buffett list pages

## Files to Consider
### Backend
- `dotnet/Stocks.Persistence/Database/Statements/GetPricesByTickerStmt.cs` — existing price query pattern
- `dotnet/Stocks.Persistence/Database/Statements/GetAllLatestPricesStmt.cs` — latest price query
- `dotnet/Stocks.Persistence/Services/ScoringService.cs` — Graham scoring computation
- `dotnet/Stocks.Persistence/Services/MoatScoringService.cs` — Buffett scoring, CAGR pattern
- `dotnet/Stocks.WebApi/Endpoints/ScoringEndpoints.cs` — Graham detail endpoint
- `dotnet/Stocks.WebApi/Endpoints/MoatScoringEndpoints.cs` — Buffett detail endpoint
- `dotnet/Stocks.WebApi/Endpoints/ReportEndpoints.cs` — Graham list endpoint
- `dotnet/Stocks.WebApi/Endpoints/MoatReportEndpoints.cs` — Buffett list endpoint
- `dotnet/Stocks.DataModels/Scoring/CompanyScoreSummary.cs` — Graham list DTO
- `dotnet/Stocks.DataModels/Scoring/CompanyMoatScoreSummary.cs` — Buffett list DTO
- `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyScoresStmt.cs` — Graham batch insert
- `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyMoatScoresStmt.cs` — Buffett batch insert
- `dotnet/Stocks.Persistence/Database/Statements/GetCompanyScoresStmt.cs` — Graham list query
- `dotnet/Stocks.Persistence/Database/Statements/GetCompanyMoatScoresStmt.cs` — Buffett list query
- `dotnet/Stocks.EDGARScraper/Program.cs` — CLI command wiring, batch score commands
- `dotnet/Stocks.Shared/ErrorCodes.cs` — error code enum
- `dotnet/Stocks.DataModels/PriceRow.cs` — price data model

### Frontend
- `frontend/stocks-frontend/src/app/features/scoring/scoring.component.ts` — Graham detail
- `frontend/stocks-frontend/src/app/features/moat-scoring/moat-scoring.component.ts` — Buffett detail
- `frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.ts` — Graham list
- `frontend/stocks-frontend/src/app/features/moat-scores-report/moat-scores-report.component.ts` — Buffett list
- `frontend/stocks-frontend/src/app/core/services/api.service.ts` — API client
- `frontend/stocks-frontend/src/app/app.routes.ts` — routing

### Migrations
- `dotnet/Stocks.Persistence/Database/Migrations/V013__AddCompanyScoresTable.sql` — Graham scores DDL
- `dotnet/Stocks.Persistence/Database/Migrations/V017__AddCompanyMoatScoresTable.sql` — Buffett scores DDL

## Output
Write plan to `.prompts/investment-return-report-plan/plan.md`:
- Ordered checkpoints (implementation + tests each — no checkpoint without tests unless it is purely non-code work like documentation or configuration)
- Files to create/modify per checkpoint
- Metadata block (Status, Dependencies, Open Questions, Assumptions)

<!-- Self-review: converged after 1 pass -->
