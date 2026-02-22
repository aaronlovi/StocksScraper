# Plan: Remove return1y Column and Create Investment Return Report Pages

## Table of Contents

1. [Metadata](#1-metadata)
2. [Checkpoint 1 — Remove return1y from Backend](#2-checkpoint-1--remove-return1y-from-backend)
3. [Checkpoint 2 — Remove Per-Company Investment Return Infrastructure](#3-checkpoint-2--remove-per-company-investment-return-infrastructure)
4. [Checkpoint 3 — Remove return1y and Investment Return from Frontend](#4-checkpoint-3--remove-return1y-and-investment-return-from-frontend)
5. [Checkpoint 4 — New Backend: Return Computation Service and Report Endpoints](#5-checkpoint-4--new-backend-return-computation-service-and-report-endpoints)
6. [Checkpoint 5 — New Frontend: Report Components, Routes, Sidebar](#6-checkpoint-5--new-frontend-report-components-routes-sidebar)

## 1. Metadata

- **Status**: complete
- **Dependencies**: None (all dependencies verified during research)
- **Open Questions**: None
- **Assumptions**:
  - Total scored companies is small (~50-200), so in-memory sort + paginate is performant
  - The `prices` table is indexed on `(ticker, price_date DESC)` for efficient `DISTINCT ON` queries
  - Evolve migration tooling requires V018 to remain (only add V019 drop-column migration)
  - Existing report components (`ScoresReportComponent`, `MoatScoresReportComponent`) have no unit test spec files; new report components follow the same convention

---

## 2. Checkpoint 1 — Remove return1y from Backend

Remove all `return1y` / `Return1y` / `return_1y` references from backend DTOs, enums, database statements, sort mappings, enrichment pipeline code, and tests. Create a drop-column migration.

### 2.1. Checkpoint 1 — Build

**DTOs and Enums** — remove `Return1y` parameter/field:
- `dotnet/Stocks.DataModels/Scoring/CompanyScoreSummary.cs` — remove `decimal? Return1y` record parameter
- `dotnet/Stocks.DataModels/Scoring/CompanyMoatScoreSummary.cs` — remove `decimal? Return1y` record parameter
- `dotnet/Stocks.DataModels/Scoring/ScoresReportRequest.cs` — remove `Return1y` from `ScoresSortBy` enum
- `dotnet/Stocks.DataModels/Scoring/MoatScoresReportRequest.cs` — remove `Return1y` from `MoatScoresSortBy` enum

**Bulk Insert Statements** — remove `return_1y` column and write call:
- `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyScoresStmt.cs` — remove `return_1y` from COPY column list (line 23), remove `WriteNullableAsync(s.Return1y, ...)` (line 56)
- `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyMoatScoresStmt.cs` — remove `return_1y` from COPY column list (line 21), remove `WriteNullableAsync(s.Return1y, ...)` (line 48)

**Query Statements** — remove `_return1yIndex` field, sort mapping case, SELECT column, ordinal lookup, and `ProcessCurrentRow` mapping:
- `dotnet/Stocks.Persistence/Database/Statements/GetCompanyScoresStmt.cs` — lines 43, 73, 103, 139, 201
- `dotnet/Stocks.Persistence/Database/Statements/GetCompanyMoatScoresStmt.cs` — lines 37, 66, 94, 124, 180

**In-Memory Data** — remove `Return1y` sort cases:
- `dotnet/Stocks.Persistence/Database/DbmInMemoryData.cs` — remove `MoatScoresSortBy.Return1y` case (~line 813), remove `ScoresSortBy.Return1y` case (~line 826)

**Endpoint Sort Mappings** — remove `return1y` string-to-enum mapping:
- `dotnet/Stocks.WebApi/Endpoints/ReportEndpoints.cs` — remove `"return1y" => ScoresSortBy.Return1y` (lines 69-70)
- `dotnet/Stocks.WebApi/Endpoints/MoatReportEndpoints.cs` — remove `"return1y" => MoatScoresSortBy.Return1y` (lines 67-68)

**Enrichment Pipeline** — remove return1y enrichment from batch scoring:
- `dotnet/Stocks.EDGARScraper/Program.cs` — remove enrichment log + call for Graham scores (lines 990-992), remove enrichment log + call for moat scores (lines 1019-1021), remove `ComputeReturn1yByTicker`, `EnrichScoresWithReturn1y`, `EnrichMoatScoresWithReturn1y` methods (lines 1037-1101)

**Migration** — create drop-column migration:
- Create `dotnet/Stocks.Persistence/Database/Migrations/V019__DropReturn1yFromScoreTables.sql`:
  ```sql
  ALTER TABLE company_scores DROP COLUMN IF EXISTS return_1y;
  ALTER TABLE company_moat_scores DROP COLUMN IF EXISTS return_1y;
  ```

### 2.2. Checkpoint 1 — Test

- Delete `dotnet/Stocks.EDGARScraper.Tests/Scoring/Return1yEnrichmentTests.cs` (entire file)
- Update `dotnet/Stocks.EDGARScraper.Tests/Scoring/MoatScoringModelTests.cs` — remove `Return1y: null` from `CompanyMoatScoreSummary` constructor (line 125)
- All remaining tests must pass: `dotnet test dotnet/EDGARScraper.sln`

### 2.3. Checkpoint 1 — Verify

- `dotnet build dotnet/EDGARScraper.sln` — clean build, no warnings
- `dotnet test dotnet/EDGARScraper.sln` — all tests pass
- Grep for `return1y`, `Return1y`, `return_1y` in `dotnet/` — no remaining references (except V018 migration, which must stay)

---

## 3. Checkpoint 2 — Remove Per-Company Investment Return Infrastructure

Delete the per-company investment return endpoint, service, result DTO, per-ticker price statements, and their interface/implementation/in-memory counterparts.

### 3.1. Checkpoint 2 — Build

**Delete entire files:**
- `dotnet/Stocks.WebApi/Endpoints/InvestmentReturnEndpoints.cs`
- `dotnet/Stocks.Persistence/Services/InvestmentReturnService.cs`
- `dotnet/Stocks.DataModels/Scoring/InvestmentReturnResult.cs`
- `dotnet/Stocks.Persistence/Database/Statements/GetPriceNearDateStmt.cs`
- `dotnet/Stocks.Persistence/Database/Statements/GetLatestPriceByTickerStmt.cs`

**Remove interface methods:**
- `dotnet/Stocks.Persistence/Database/IDbmService.cs` — remove `GetPriceNearDate(string ticker, DateOnly targetDate, CancellationToken ct)` and `GetLatestPriceByTicker(string ticker, CancellationToken ct)` (lines 112-113)

**Remove implementations:**
- `dotnet/Stocks.Persistence/Database/DbmService.cs` — remove `GetPriceNearDate()` and `GetLatestPriceByTicker()` implementations
- `dotnet/Stocks.Persistence/Database/DbmInMemoryService.cs` — remove `GetPriceNearDate()` and `GetLatestPriceByTicker()` implementations
- `dotnet/Stocks.Persistence/Database/DbmInMemoryData.cs` — remove per-ticker price lookup backing methods/data

**Remove DI and endpoint wiring:**
- `dotnet/Stocks.WebApi/Program.cs` — remove `AddSingleton<InvestmentReturnService>()` (line 25), remove `app.MapInvestmentReturnEndpoints()` (line 55)

### 3.2. Checkpoint 2 — Test

- Delete `dotnet/Stocks.EDGARScraper.Tests/Scoring/InvestmentReturnServiceTests.cs` (entire file)
- All remaining tests must pass: `dotnet test dotnet/EDGARScraper.sln`

### 3.3. Checkpoint 2 — Verify

- `dotnet build dotnet/EDGARScraper.sln` — clean build, no warnings
- `dotnet test dotnet/EDGARScraper.sln` — all tests pass
- Grep for `InvestmentReturnService`, `InvestmentReturnEndpoints`, `GetPriceNearDate`, `GetLatestPriceByTicker`, `InvestmentReturnResult` in `dotnet/` — no remaining references

---

## 4. Checkpoint 3 — Remove return1y and Investment Return from Frontend

Remove `return1y` from report page interfaces and table columns. Remove inline investment return sections from Graham and Buffett detail pages. Remove the `InvestmentReturnResponse` interface and `getInvestmentReturn()` API method.

### 4.1. Checkpoint 3 — Build

**API Service** (`frontend/stocks-frontend/src/app/core/services/api.service.ts`):
- Remove `return1y: number | null` from `CompanyScoreSummary` interface (~line 126)
- Remove `return1y: number | null` from `CompanyMoatScoreSummary` interface (~line 235)
- Remove `InvestmentReturnResponse` interface (lines 203-212)
- Remove `getInvestmentReturn()` method (lines 302-307)

**Scores Report** (`frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.ts`):
- Remove `return1y` column header (`<th>`) with sort handler (lines 86-88)
- Remove `return1y` table cell (`<td>`) (line 112)
- Remove `fmtReturn()` and `returnClass()` methods (lines 309-318)

**Moat Scores Report** (`frontend/stocks-frontend/src/app/features/moat-scores-report/moat-scores-report.component.ts`):
- Remove `return1y` column header (`<th>`) with sort handler (lines 84-86)
- Remove `return1y` table cell (`<td>`) (line 110)
- Remove `fmtReturn()` and `returnClass()` methods (lines 307-316)

**Graham Detail Page** (`frontend/stocks-frontend/src/app/features/scoring/scoring.component.ts`):
- Remove `<h3>Investment Return</h3>` template block (lines ~123-170)
- Remove signals: `investmentReturn`, `investmentReturnLoading`, `investmentReturnError`, `investmentStartDate`
- Remove `investmentReturnDate$` Subject
- Remove ngOnInit subscription (lines 561-580) and initial `loadInvestmentReturn()` call
- Remove `onStartDateChange()`, `loadInvestmentReturn()` methods
- Remove `defaultStartDate()` helper function
- Remove `.investment-return-section` CSS styles (lines ~415-436)
- Remove now-unused imports (`InvestmentReturnResponse`, `Subject`, `switchMap`, `catchError`, `EMPTY`, `takeUntilDestroyed`, `inject`, `DestroyRef`) — only remove imports that are no longer used elsewhere in the file

**Buffett Detail Page** (`frontend/stocks-frontend/src/app/features/moat-scoring/moat-scoring.component.ts`):
- Same removals as Graham detail page above

### 4.2. Checkpoint 3 — Test

- Update `frontend/stocks-frontend/src/app/features/scoring/scoring.component.spec.ts`:
  - Remove investment return HTTP mock from `flushRequests()` (lines ~96-107)
  - Remove "should display investment return data" test (lines ~163-173)
  - Remove any other investment return expectations (line ~189)
- `cd frontend/stocks-frontend && npx ng build` — clean build
- `cd frontend/stocks-frontend && npx ng test --watch=false` — all tests pass (if test runner is configured)

### 4.3. Checkpoint 3 — Verify

- Frontend builds without errors
- Grep for `return1y`, `Return1y`, `InvestmentReturnResponse`, `getInvestmentReturn`, `investmentReturn`, `investment-return` in `frontend/` — no remaining references
- Existing frontend tests pass

---

## 5. Checkpoint 4 — New Backend: Return Computation Service and Report Endpoints

Create new DTOs, a return computation service, and two report endpoints (`/api/reports/graham-returns`, `/api/reports/buffett-returns`). The service fetches all scores (using existing `GetCompanyScores`/`GetCompanyMoatScores` with a large page size), fetches batch prices, computes returns, sorts by the requested column, and paginates in memory.

### 5.1. Checkpoint 4 — Build

**New DTO** — create `dotnet/Stocks.DataModels/Scoring/CompanyScoreReturnSummary.cs`:
```csharp
public record CompanyScoreReturnSummary(
    ulong CompanyId,
    string Cik,
    string? CompanyName,
    string? Ticker,
    string? Exchange,
    int OverallScore,
    int ComputableChecks,
    decimal? PricePerShare,
    decimal? TotalReturnPct,
    decimal? AnnualizedReturnPct,
    decimal? CurrentValueOf1000,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal? StartPrice,
    decimal? EndPrice,
    DateTime ComputedAt);
```

**New Request Types** — create `dotnet/Stocks.DataModels/Scoring/ReturnsReportRequest.cs`:
```csharp
public enum ReturnsReportSortBy
{
    OverallScore,
    TotalReturnPct,
    AnnualizedReturnPct,
    CurrentValueOf1000
}

public record ReturnsReportFilter(int? MinScore, int? MaxScore, string? Exchange);
```

**New Service** — create `dotnet/Stocks.Persistence/Services/InvestmentReturnReportService.cs`:
- Constructor: inject `IDbmService`
- Public method `GetGrahamReturns(DateOnly startDate, PaginationRequest pagination, ReturnsReportSortBy sortBy, SortDirection sortDir, ReturnsReportFilter? filter, CancellationToken ct)` returning `Task<Result<PagedResults<CompanyScoreReturnSummary>>>`
- Public method `GetBuffettReturns(...)` — same signature
- Private shared logic:
  1. Fetch all scores via existing `dbm.GetCompanyScores()` (or `GetCompanyMoatScores()`) with a large page size (10000) and default sort (OverallScore DESC). Pass filter as `ScoresFilter` (reuse MinScore/MaxScore/Exchange).
  2. Fetch batch prices: `dbm.GetAllPricesNearDate(startDate, ct)` and `dbm.GetAllLatestPrices(ct)`
  3. Build `Dictionary<string, LatestPrice>` lookups for start prices and end prices (by ticker, using explicit loops — no LINQ)
  4. For each score, look up start/end prices by ticker. If both exist and start price > 0, compute:
     - `totalReturnPct = (endPrice / startPrice - 1m) * 100m`
     - `annualizedReturnPct`: compute days between start and end date; if days > 0, `(Math.Pow((double)(endPrice / startPrice), 365.25 / days) - 1.0) * 100.0`; if days == 0, null
     - `currentValueOf1000 = 1000m * endPrice / startPrice`
     - Round return values to 2 decimal places
  5. If either price is missing or start price <= 0, set all return fields to null
  6. Build `List<CompanyScoreReturnSummary>` from the enriched data
  7. Sort in memory by the requested `ReturnsReportSortBy` column. Nulls sort last. Secondary sort by CompanyId for stability.
  8. Paginate: compute offset from `pagination.PageNumber` and `pagination.PageSize`, extract the page slice using explicit loop
  9. Return `PagedResults<CompanyScoreReturnSummary>` with `PaginationResponse`

**New Endpoints** — create `dotnet/Stocks.WebApi/Endpoints/GrahamReturnsEndpoints.cs`:
```csharp
public static class GrahamReturnsEndpoints
{
    public static void MapGrahamReturnsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reports/graham-returns", async (
            string? startDate,
            uint? page, uint? pageSize,
            string? sortBy, string? sortDir,
            int? minScore, string? exchange,
            InvestmentReturnReportService service,
            CancellationToken ct) =>
        {
            // Parse startDate (default: 1 year ago), parse sort enum, build filter
            // Call service.GetGrahamReturns(...)
            // return result.ToHttpResult()
        });
    }
}
```

Create `dotnet/Stocks.WebApi/Endpoints/BuffettReturnsEndpoints.cs` — same pattern, calls `service.GetBuffettReturns(...)`.

**Wire up** — modify `dotnet/Stocks.WebApi/Program.cs`:
- Add `builder.Services.AddSingleton<InvestmentReturnReportService>()` in the DI block
- Add `app.MapGrahamReturnsEndpoints()` and `app.MapBuffettReturnsEndpoints()` in the endpoint mapping block

### 5.2. Checkpoint 4 — Test

Create `dotnet/Stocks.EDGARScraper.Tests/Scoring/InvestmentReturnReportServiceTests.cs`:
- Test positive return computation: seed scores and prices, verify totalReturnPct, annualizedReturnPct, currentValueOf1000
- Test negative return computation
- Test missing start price: return fields should be null
- Test missing end price: return fields should be null
- Test zero start price: return fields should be null (guard against division by zero)
- Test same-day: totalReturnPct = 0, annualizedReturnPct = null
- Test sorting by TotalReturnPct (descending, nulls last)
- Test sorting by OverallScore
- Test pagination (page 1 and page 2 with small page size)
- Test filter by minScore and exchange

Use `DbmInMemoryService` / `DbmInMemoryData` for test isolation, following the existing test patterns.

### 5.3. Checkpoint 4 — Verify

- `dotnet build dotnet/EDGARScraper.sln` — clean build, no warnings
- `dotnet test dotnet/EDGARScraper.sln` — all tests pass (existing + new)

---

## 6. Checkpoint 5 — New Frontend: Report Components, Routes, Sidebar

Create two new standalone Angular components for Graham Returns and Buffett Returns report pages. Add API service methods, routes, and sidebar links.

### 6.1. Checkpoint 5 — Build

**API Service** (`frontend/stocks-frontend/src/app/core/services/api.service.ts`):
- Add `CompanyScoreReturnSummary` interface:
  ```typescript
  export interface CompanyScoreReturnSummary {
    companyId: number;
    cik: string;
    companyName: string | null;
    ticker: string | null;
    exchange: string | null;
    overallScore: number;
    computableChecks: number;
    pricePerShare: number | null;
    totalReturnPct: number | null;
    annualizedReturnPct: number | null;
    currentValueOf1000: number | null;
    startDate: string | null;
    endDate: string | null;
    startPrice: number | null;
    endPrice: number | null;
    computedAt: string;
  }
  ```
- Add `ReturnsReportParams` interface:
  ```typescript
  export interface ReturnsReportParams {
    startDate: string;
    page: number;
    pageSize: number;
    sortBy: string;
    sortDir: string;
    minScore: number | null;
    exchange: string | null;
  }
  ```
- Add `getGrahamReturns(params: ReturnsReportParams): Observable<PaginatedResponse<CompanyScoreReturnSummary>>` method — builds URL with query params to `/api/reports/graham-returns`
- Add `getBuffettReturns(params: ReturnsReportParams): Observable<PaginatedResponse<CompanyScoreReturnSummary>>` method — same pattern for `/api/reports/buffett-returns`

**Graham Returns Report Component** — create `frontend/stocks-frontend/src/app/features/graham-returns-report/graham-returns-report.component.ts`:
- Standalone component with `FormsModule` and `RouterLink` imports
- Follow `ScoresReportComponent` pattern exactly for structure
- Signals: `items`, `pagination`, `loading`, `error`, `computedAt` (same as existing reports)
- Properties: `page`, `pageSize`, `sortBy` (default: `'overallScore'`), `sortDir` (default: `'desc'`), `minScore`, `exchange`, `startDate` (default: 1 year ago as `YYYY-MM-DD` string)
- Methods: `toggleSort()`, `sortIndicator()`, `onFilterChange()`, `goToPage()`, `onStartDateChange()`, `fetchReturns()`, `scoreBadgeClass()`, `rowHighlightClass()`
- Formatting methods: `fmtPrice()` (for price/currency columns), `fmtReturn()` (for return % columns, +/- sign, 2 decimals), `returnClass()` (positive=green, negative=red), `fmtCurrency()` (for $1,000 invested column)
- Template:
  - Title: "Graham Returns"
  - Filters row: Min Score select, Exchange select, Page Size select, **Start Date date input** (default 1 year ago)
  - `onStartDateChange()`: update `startDate`, reset page to 1, call `fetchReturns()`
  - Sortable table columns: Score, Company (link to `/company/:cik/scoring`), Ticker, Exchange, Price, Total Return %, Annualized Return %, $1,000 Invested
  - Pagination at bottom (same as existing reports)
  - "Scores computed at" timestamp
- CSS: reuse existing report styles, add `.positive { color: green; }` and `.negative { color: red; }` for return columns

**Buffett Returns Report Component** — create `frontend/stocks-frontend/src/app/features/buffett-returns-report/buffett-returns-report.component.ts`:
- Same structure as Graham Returns Report but:
  - Title: "Buffett Returns"
  - Calls `api.getBuffettReturns(...)` instead
  - Company link goes to `/company/:cik/moat-scoring`

**Routes** (`frontend/stocks-frontend/src/app/app.routes.ts`):
- Add route: `{ path: 'graham-returns', title: 'Stocks - Graham Returns', loadComponent: () => import('./features/graham-returns-report/graham-returns-report.component').then(m => m.GrahamReturnsReportComponent) }`
- Add route: `{ path: 'buffett-returns', title: 'Stocks - Buffett Returns', loadComponent: () => import('./features/buffett-returns-report/buffett-returns-report.component').then(m => m.BuffettReturnsReportComponent) }`

**Sidebar** (`frontend/stocks-frontend/src/app/core/layout/sidebar/sidebar.component.ts`):
- Add `<li>` after Graham Scores link: `<li><a routerLink="/graham-returns" routerLinkActive="active">Graham Returns</a></li>`
- Add `<li>` after Buffett Scores link: `<li><a routerLink="/buffett-returns" routerLinkActive="active">Buffett Returns</a></li>`

### 6.2. Checkpoint 5 — Test

- `cd frontend/stocks-frontend && npx ng build` — clean build
- No new component spec files (following existing report component pattern — `ScoresReportComponent` and `MoatScoresReportComponent` do not have spec files)
- Existing frontend tests still pass

### 6.3. Checkpoint 5 — Verify

- Frontend builds without errors
- New routes resolve correctly (manual verification: navigate to `/graham-returns` and `/buffett-returns`)
- Sidebar shows new links
- Date picker defaults to 1 year ago
- Table renders with correct columns and formatting

<!-- Self-review: converged after 1 pass -->
