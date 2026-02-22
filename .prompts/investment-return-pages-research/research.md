# Research: Investment Return Report Pages

## Table of Contents

1. [What Code to Keep vs Remove](#1-what-code-to-keep-vs-remove)
2. [Backend Endpoint Design](#2-backend-endpoint-design)
3. [New Page UI Design](#3-new-page-ui-design)
4. [Date Picker Behavior](#4-date-picker-behavior)
5. [Complete return1y Removal Checklist](#5-complete-return1y-removal-checklist)
6. [Existing Patterns to Follow](#6-existing-patterns-to-follow)
7. [Risks and Concerns](#7-risks-and-concerns)
8. [Recommended Approach](#8-recommended-approach)
9. [Metadata](#9-metadata)

## 1. What Code to Keep vs Remove

### 1.1. What Code to Keep vs Remove - Keep (Reusable Backend)

These were added in commit `2ac0fe8` and are reusable for the new batch return pages:

| File | What It Does | Why Keep |
|------|-------------|----------|
| `dotnet/Stocks.Persistence/Database/Statements/GetAllPricesNearDateStmt.cs` | Batch lookup: `SELECT DISTINCT ON (ticker) ticker, close, price_date FROM prices WHERE price_date <= @target_date ORDER BY ticker, price_date DESC` | New pages need prices near user-selected start date for all tickers at once |
| `dotnet/Stocks.Persistence/Database/Statements/GetAllLatestPricesStmt.cs` | Batch lookup: latest price per ticker (no date filter) | New pages need current prices for all tickers to compute returns |
| `dotnet/Stocks.DataModels/Scoring/LatestPrice.cs` | `record LatestPrice(string Ticker, decimal Close, DateOnly PriceDate)` | Result type used by both batch price stmts |
| `dotnet/Stocks.Persistence/Database/IDbmService.cs` (lines 83-84) | `GetAllLatestPrices()` and `GetAllPricesNearDate(DateOnly)` interface methods | Needed by new endpoint |
| `dotnet/Stocks.Persistence/Database/DbmService.cs` | Implementations of above | Needed by new endpoint |
| `dotnet/Stocks.Persistence/Database/DbmInMemoryService.cs` | In-memory implementations | Needed for testing |

### 1.2. What Code to Keep vs Remove - Remove (return1y Column Plumbing)

All `return_1y` / `Return1y` references across the codebase. Full file list in [Section 5](#5-complete-return1y-removal-checklist).

### 1.3. What Code to Keep vs Remove - Remove (Inline Detail Page Sections)

The investment return sections on the Graham and Buffett detail pages:

| File | What to Remove |
|------|---------------|
| `frontend/stocks-frontend/src/app/features/scoring/scoring.component.ts` | `<h3>Investment Return</h3>` block (lines ~123-170), signals (`investmentReturn`, `investmentReturnLoading`, `investmentReturnError`, `investmentStartDate`), `investmentReturnDate$` Subject, `onStartDateChange()`, `loadInvestmentReturn()`, subscription in `ngOnInit()` (lines 561-580), CSS `.investment-return-section` styles, imports (`InvestmentReturnResponse`, `Subject`, `switchMap`, `catchError`, `EMPTY`, `takeUntilDestroyed`, `inject`, `DestroyRef`) |
| `frontend/stocks-frontend/src/app/features/moat-scoring/moat-scoring.component.ts` | Same set of template, signals, methods, styles, and imports |
| `frontend/stocks-frontend/src/app/features/scoring/scoring.component.spec.ts` | Investment-return related test expectations (lines ~97, 169, 189) |

### 1.4. What Code to Keep vs Remove - Remove (Per-Company Endpoint Infrastructure)

With inline detail page sections removed, the per-company endpoint becomes unused:

| File | What to Remove |
|------|---------------|
| `dotnet/Stocks.WebApi/Endpoints/InvestmentReturnEndpoints.cs` | Entire file |
| `dotnet/Stocks.WebApi/Program.cs` | Line 25: `AddSingleton<InvestmentReturnService>()`, Line 55: `app.MapInvestmentReturnEndpoints()` |
| `dotnet/Stocks.Persistence/Services/InvestmentReturnService.cs` | Entire file (per-ticker computation; new pages use batch approach) |
| `dotnet/Stocks.DataModels/Scoring/InvestmentReturnResult.cs` | Entire file |
| `dotnet/Stocks.Persistence/Database/Statements/GetPriceNearDateStmt.cs` | Entire file (single-ticker lookup, not used by batch) |
| `dotnet/Stocks.Persistence/Database/Statements/GetLatestPriceByTickerStmt.cs` | Entire file (single-ticker lookup, not used by batch) |
| `dotnet/Stocks.Persistence/Database/IDbmService.cs` (lines 112-113) | `GetPriceNearDate()` and `GetLatestPriceByTicker()` methods |
| `dotnet/Stocks.Persistence/Database/DbmService.cs` | Implementations of above |
| `dotnet/Stocks.Persistence/Database/DbmInMemoryService.cs` | Implementations of above |
| `dotnet/Stocks.Persistence/Database/DbmInMemoryData.cs` | In-memory backing for above |
| `frontend/stocks-frontend/src/app/core/services/api.service.ts` | `InvestmentReturnResponse` interface (lines 203-212), `getInvestmentReturn()` method (lines 302-307) |
| `dotnet/Stocks.EDGARScraper.Tests/Scoring/InvestmentReturnServiceTests.cs` | Entire test file |

**Verified**: `CompanyEndpoints.cs` does NOT reference `GetLatestPriceByTicker` or `GetPriceNearDate`. All per-company price statements can be safely deleted.

## 2. Backend Endpoint Design

### 2.1. Backend Endpoint Design - Recommended: Two New Dedicated Endpoints

Follow the existing pattern of separate Graham/Buffett endpoints:

- `GET /api/reports/graham-returns?startDate=YYYY-MM-DD&page=1&pageSize=50&sortBy=totalReturn&sortDir=desc&minScore=7&exchange=NASDAQ`
- `GET /api/reports/buffett-returns?startDate=YYYY-MM-DD&page=1&pageSize=50&sortBy=totalReturn&sortDir=desc&minScore=7&exchange=NASDAQ`

### 2.2. Backend Endpoint Design - Why Not Call Two Endpoints from Frontend

Calling the existing scores endpoint + a separate batch-return endpoint would require the frontend to merge data client-side. This breaks server-side sorting (can't sort by return columns) and pagination. A combined endpoint is cleaner.

### 2.3. Backend Endpoint Design - Implementation Approach

Each endpoint:

1. Fetch all scores (from `company_scores` or `company_moat_scores`) with existing sort/filter/pagination
2. Fetch batch prices: `GetAllPricesNearDate(startDate)` + `GetAllLatestPrices()`
3. Join prices by ticker, compute `totalReturnPct`, `annualizedReturnPct`, `currentValueOf1000` per company
4. Return combined response

**Important consideration for sorting**: If the user wants to sort by return columns (totalReturn, annualizedReturn, $1000 value), the computation must happen BEFORE pagination. This means:

- Option A: Fetch ALL scores (unpaginated), compute returns, sort, then paginate in memory. Feasible since total scored companies are ~50-200.
- Option B: Pre-compute returns and store them (like the current `return_1y` approach, but this is what we're removing).

**Recommendation**: Option A. The dataset is small enough (50-200 companies per score type) that in-memory sort + paginate is fine. The batch price queries (`GetAllPricesNearDate`, `GetAllLatestPrices`) return one row per ticker using `DISTINCT ON`, which is efficient.

### 2.4. Backend Endpoint Design - Performance

- 2 DB queries for prices (batch, indexed): ~10-50ms each
- 1 DB query for scores: already fast (existing pattern)
- In-memory join + computation for ~200 companies: negligible
- Total: ~50-150ms per request, acceptable for interactive use

### 2.5. Backend Endpoint Design - Response Shape

```json
{
  "items": [
    {
      "companyId": 123,
      "cik": "320193",
      "companyName": "APPLE INC",
      "ticker": "AAPL",
      "exchange": "NASDAQ",
      "overallScore": 12,
      "computableChecks": 15,
      "pricePerShare": 185.50,
      "totalReturnPct": 42.35,
      "annualizedReturnPct": 18.72,
      "currentValueOf1000": 1423.50,
      "startDate": "2024-02-21",
      "endDate": "2026-02-20",
      "startPrice": 130.25,
      "endPrice": 185.50
    }
  ],
  "pagination": {
    "pageNumber": 1,
    "totalItems": 87,
    "totalPages": 2
  }
}
```

## 3. New Page UI Design

### 3.1. New Page UI Design - Layout

Follow the existing `ScoresReportComponent` / `MoatScoresReportComponent` pattern exactly:
- Title at top
- Filters row (Min Score, Exchange, Page Size, **Date Picker** — new)
- Sortable table
- Pagination at bottom
- "Scores computed" timestamp

### 3.2. New Page UI Design - Graham Returns Columns

| Column | Sortable | Source |
|--------|----------|--------|
| Score | Yes (`overallScore`) | From `company_scores` |
| Company | No (link to `/company/:cik/scoring`) | From `company_scores` |
| Ticker | No | From `company_scores` |
| Exchange | No | From `company_scores` |
| Price | No | Current price from latest prices |
| Total Return % | Yes (`totalReturnPct`) | Computed from prices |
| Annualized Return % | Yes (`annualizedReturnPct`) | Computed from prices |
| $1,000 Invested | Yes (`currentValueOf1000`) | Computed from prices |

### 3.3. New Page UI Design - Buffett Returns Columns

Same as Graham but links go to `/company/:cik/moat-scoring` and scores come from `company_moat_scores`.

### 3.4. New Page UI Design - Formatting

Carry over from existing report components:
- `fmtPrice()` for price columns (`$185.50`)
- Score badge with green/yellow/red coloring
- Row highlighting for perfect/near-perfect scores
- New: `fmtReturn()` and `returnClass()` for return columns (positive=green, negative=red, with +/- sign)
- New: `fmtCurrency()` for $1,000 invested column

### 3.5. New Page UI Design - New Routes and Sidebar

Routes (`app.routes.ts`):
```typescript
{ path: 'graham-returns', title: 'Stocks - Graham Returns',
  loadComponent: () => import('./features/graham-returns-report/graham-returns-report.component')
    .then(m => m.GrahamReturnsReportComponent) },
{ path: 'buffett-returns', title: 'Stocks - Buffett Returns',
  loadComponent: () => import('./features/buffett-returns-report/buffett-returns-report.component')
    .then(m => m.BuffettReturnsReportComponent) },
```

Sidebar (`sidebar.component.ts`): Add two new `<li>` entries after the existing score links:
```html
<li><a routerLink="/graham-returns" routerLinkActive="active">Graham Returns</a></li>
<li><a routerLink="/buffett-returns" routerLinkActive="active">Buffett Returns</a></li>
```

## 4. Date Picker Behavior

### 4.1. Date Picker Behavior - Single Start Date

The user picks a single start date. The "end" is always the latest available price (most recent market close). This matches the existing `InvestmentReturnEndpoints` pattern where `startDate` defaults to 1 year ago.

### 4.2. Date Picker Behavior - Default Date

Default: 1 year ago from today (`new Date()` minus 1 year), same as the removed inline sections used (`defaultStartDate()` computed `today.getFullYear() - 1`).

### 4.3. Date Picker Behavior - Interaction with Pagination and Sorting

When the date changes:
1. Frontend calls the API with the new `startDate` + current sort/filter/page params
2. Backend computes returns for ALL scored companies, sorts, paginates, returns result
3. Frontend replaces the table data
4. Page resets to 1 on date change (same as filter changes)

The `startDate` is a query parameter on the new endpoint, alongside existing params (`page`, `pageSize`, `sortBy`, `sortDir`, `minScore`, `exchange`).

### 4.4. Date Picker Behavior - HTML Implementation

```html
<label>
  Start Date
  <input type="date" [value]="startDate" (change)="onStartDateChange($event)" />
</label>
```

Placed in the `.filters` div alongside Min Score, Exchange, and Page Size.

## 5. Complete return1y Removal Checklist

Every file that references `return1y`, `Return1y`, or `return_1y`:

### 5.1. Complete return1y Removal Checklist - Backend (DTOs and Enums)

| File | Change |
|------|--------|
| `dotnet/Stocks.DataModels/Scoring/CompanyScoreSummary.cs:32` | Remove `decimal? Return1y` parameter |
| `dotnet/Stocks.DataModels/Scoring/CompanyMoatScoreSummary.cs:26` | Remove `decimal? Return1y` parameter |
| `dotnet/Stocks.DataModels/Scoring/ScoresReportRequest.cs:15` | Remove `Return1y` from `ScoresSortBy` enum |
| `dotnet/Stocks.DataModels/Scoring/MoatScoresReportRequest.cs:14` | Remove `Return1y` from `MoatScoresSortBy` enum |

### 5.2. Complete return1y Removal Checklist - Backend (Database Statements)

| File | Change |
|------|--------|
| `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyScoresStmt.cs:23` | Remove `return_1y` from COPY column list |
| `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyScoresStmt.cs:56` | Remove `WriteNullableAsync(s.Return1y, ...)` |
| `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyMoatScoresStmt.cs:21` | Remove `return_1y` from COPY column list |
| `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyMoatScoresStmt.cs:48` | Remove `WriteNullableAsync(s.Return1y, ...)` |
| `dotnet/Stocks.Persistence/Database/Statements/GetCompanyScoresStmt.cs:43,73,103,139,201` | Remove `_return1yIndex` field, sort mapping, SELECT column, ordinal lookup, `ProcessCurrentRow` mapping |
| `dotnet/Stocks.Persistence/Database/Statements/GetCompanyMoatScoresStmt.cs:37,66,94,124,180` | Same as above |

### 5.3. Complete return1y Removal Checklist - Backend (Service Layer and Endpoints)

| File | Change |
|------|--------|
| `dotnet/Stocks.Persistence/Database/DbmInMemoryData.cs:813` | Remove `MoatScoresSortBy.Return1y` case |
| `dotnet/Stocks.Persistence/Database/DbmInMemoryData.cs:826` | Remove `ScoresSortBy.Return1y` case |
| `dotnet/Stocks.WebApi/Endpoints/ReportEndpoints.cs:69-70` | Remove `return1y` -> `ScoresSortBy.Return1y` mapping |
| `dotnet/Stocks.WebApi/Endpoints/MoatReportEndpoints.cs:67-68` | Remove `return1y` -> `MoatScoresSortBy.Return1y` mapping |
| `dotnet/Stocks.EDGARScraper/Program.cs:990-992` | Remove enrichment call and log line for Graham scores |
| `dotnet/Stocks.EDGARScraper/Program.cs:1019-1021` | Remove enrichment call and log line for moat scores |
| `dotnet/Stocks.EDGARScraper/Program.cs:1037-1101` | Remove `ComputeReturn1yByTicker`, `EnrichScoresWithReturn1y`, `EnrichMoatScoresWithReturn1y` methods |

### 5.4. Complete return1y Removal Checklist - Database Migration

| File | Change |
|------|--------|
| `dotnet/Stocks.Persistence/Database/Migrations/V018__AddReturn1yToScoreTables.sql` | **Do NOT delete** (Evolve tracks applied migrations). Create `V019__DropReturn1yFromScoreTables.sql`: `ALTER TABLE company_scores DROP COLUMN return_1y; ALTER TABLE company_moat_scores DROP COLUMN return_1y;` |

### 5.5. Complete return1y Removal Checklist - Tests

| File | Change |
|------|--------|
| `dotnet/Stocks.EDGARScraper.Tests/Scoring/Return1yEnrichmentTests.cs` | Delete entire file |
| `dotnet/Stocks.EDGARScraper.Tests/Scoring/MoatScoringModelTests.cs:125` | Remove `Return1y: null` from test data constructor |

### 5.6. Complete return1y Removal Checklist - Frontend

| File | Change |
|------|--------|
| `frontend/stocks-frontend/src/app/core/services/api.service.ts:126` | Remove `return1y: number \| null` from `CompanyScoreSummary` |
| `frontend/stocks-frontend/src/app/core/services/api.service.ts:235` | Remove `return1y: number \| null` from `CompanyMoatScoreSummary` |
| `frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.ts:86-88` | Remove `return1y` column header |
| `frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.ts:112` | Remove `return1y` table cell |
| `frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.ts:309-318` | Remove `fmtReturn()` and `returnClass()` methods |
| `frontend/stocks-frontend/src/app/features/moat-scores-report/moat-scores-report.component.ts:84-86` | Remove `return1y` column header |
| `frontend/stocks-frontend/src/app/features/moat-scores-report/moat-scores-report.component.ts:110` | Remove `return1y` table cell |
| `frontend/stocks-frontend/src/app/features/moat-scores-report/moat-scores-report.component.ts:307-316` | Remove `fmtReturn()` and `returnClass()` methods |

## 6. Existing Patterns to Follow

### 6.1. Existing Patterns to Follow - Report Endpoint Pattern

`ReportEndpoints.cs` and `MoatReportEndpoints.cs` demonstrate the pattern:
- Minimal API with query params for pagination, sort, filter
- Parse sort column from string to enum
- Call `IDbmService` method with pagination, sort, direction, filter
- Return `Result<PagedResults<T>>` via `.ToHttpResult()`

### 6.2. Existing Patterns to Follow - Frontend Report Component Pattern

`ScoresReportComponent` and `MoatScoresReportComponent` demonstrate:
- Angular standalone component with `FormsModule` and `RouterLink`
- Signals for `items`, `pagination`, `loading`, `error`, `computedAt`
- Class properties for `page`, `pageSize`, `sortBy`, `sortDir`, `minScore`, `exchange`
- `fetchScores()` method calling `ApiService`
- Template: filters row > loading/error/empty states > table > pagination > computed-at

### 6.3. Existing Patterns to Follow - Statement Pattern

New DB statements follow `QueryDbStmtBase`:
- SQL as `const string`
- `GetBoundParameters()` for SQL params
- `BeforeRowProcessing()` for ordinal caching
- `ProcessCurrentRow()` for mapping results

### 6.4. Existing Patterns to Follow - API Service Pattern

`ApiService` methods build URL with query params, call `http.get<T>()`, return `Observable<T>`.

## 7. Risks and Concerns

1. **`GetLatestPriceByTicker` dependency**: Verified that `CompanyEndpoints.cs` does NOT use this statement. Safe to delete.

2. **Migration ordering**: V018 is the latest migration. The drop-column migration should be `V019__DropReturn1yFromScoreTables.sql`.

3. **Sorting by return columns**: Requires fetching ALL scores + computing returns before paginating. With ~200 companies this is fine, but if the dataset grows to thousands, consider materialized views or pre-computation.

4. **Missing price data**: Some companies may not have price data for the selected start date (or any price data at all). These should show `null`/`N/A` for return columns and sort to the bottom when sorting by return columns.

5. **Test coverage for batch price stmts**: `GetAllPricesNearDateStmt` and `GetAllLatestPricesStmt` are currently exercised only through the batch scoring pipeline in `Program.cs`. New tests should cover the new endpoint's return computation logic.

## 8. Recommended Approach

### 8.1. Recommended Approach - Phase 1: Remove return1y

Remove all `return1y` / `Return1y` references per the checklist in Section 5. Create the drop-column migration (`V019__DropReturn1yFromScoreTables.sql`). Remove inline investment-return sections from detail pages. Remove per-company `InvestmentReturnEndpoints`, `InvestmentReturnService`, and per-ticker price statements (`GetPriceNearDateStmt`, `GetLatestPriceByTickerStmt`).

### 8.2. Recommended Approach - Phase 2: New Backend Endpoints

Create two new endpoints following the existing report pattern:
- `GET /api/reports/graham-returns` — fetches all Graham scores + batch prices, computes returns, sorts, paginates
- `GET /api/reports/buffett-returns` — same for Buffett scores

New response DTO (e.g., `CompanyScoreReturnSummary`) combining score fields + return fields.

### 8.3. Recommended Approach - Phase 3: New Frontend Pages

Two new standalone components:
- `GrahamReturnsReportComponent` at `/graham-returns`
- `BuffettReturnsReportComponent` at `/buffett-returns`

Follow existing report component pattern. Add date picker to filters row. Add sidebar links.

## 9. Metadata

### 9.1. Metadata - Status
success

### 9.2. Metadata - Dependencies
- None (all dependencies verified during research)

### 9.3. Metadata - Open Questions
- None

### 9.4. Metadata - Assumptions
- The total number of scored companies is small enough (~50-200) that computing returns in-memory before pagination is performant
- The `prices` table has an index on `(ticker, price_date DESC)` for efficient `DISTINCT ON` queries
- Evolve migration tooling requires V018 migration file to remain in place (only add a new drop-column migration)

<!-- Self-review: converged after 2 passes -->
