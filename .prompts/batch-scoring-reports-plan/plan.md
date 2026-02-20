# Plan: Batch Scoring for Top/Bottom/All Companies Reports

## Overview

Pre-compute 13-point value scores for all companies and store them in a `company_scores` summary table, enabling fast paginated/sorted/filtered reports (top, bottom, all). The batch scoring reuses existing `ScoringService` static methods (`GroupByYear`, `ComputeDerivedMetrics`, `EvaluateChecks`) to compute scores from raw data fetched in bulk.

## Checkpoints

### Checkpoint 1: Migration and Data Models

**Build:**
- Create `dotnet/Stocks.Persistence/Database/Migrations/V013__AddCompanyScoresTable.sql`:
  ```sql
  CREATE TABLE company_scores (
      company_id bigint PRIMARY KEY,
      cik bigint NOT NULL,
      company_name varchar(200),
      ticker varchar(20),
      exchange varchar(50),
      overall_score int NOT NULL,
      computable_checks int NOT NULL,
      years_of_data int NOT NULL,
      book_value decimal,
      market_cap decimal,
      debt_to_equity_ratio decimal,
      price_to_book_ratio decimal,
      debt_to_book_ratio decimal,
      adjusted_retained_earnings decimal,
      average_net_cash_flow decimal,
      average_owner_earnings decimal,
      estimated_return_cf decimal,
      estimated_return_oe decimal,
      price_per_share decimal,
      price_date date,
      shares_outstanding bigint,
      computed_at timestamptz NOT NULL DEFAULT NOW()
  );
  CREATE INDEX idx_company_scores_score ON company_scores (overall_score DESC);
  CREATE INDEX idx_company_scores_exchange ON company_scores (exchange);
  ```
- Create `dotnet/Stocks.DataModels/Scoring/CompanyScoreSummary.cs` — record type with all summary table columns:
  ```csharp
  public record CompanyScoreSummary(
      ulong CompanyId, string Cik, string? CompanyName, string? Ticker, string? Exchange,
      int OverallScore, int ComputableChecks, int YearsOfData,
      decimal? BookValue, decimal? MarketCap, decimal? DebtToEquityRatio,
      decimal? PriceToBookRatio, decimal? DebtToBookRatio,
      decimal? AdjustedRetainedEarnings, decimal? AverageNetCashFlow,
      decimal? AverageOwnerEarnings, decimal? EstimatedReturnCF, decimal? EstimatedReturnOE,
      decimal? PricePerShare, DateOnly? PriceDate, long? SharesOutstanding,
      DateTime ComputedAt);
  ```
- Create `dotnet/Stocks.DataModels/Scoring/ScoresReportRequest.cs` — request parameters for the report endpoint:
  ```csharp
  public enum ScoresSortBy { OverallScore, BookValue, MarketCap, EstimatedReturnCF, EstimatedReturnOE, DebtToEquityRatio, PriceToBookRatio }
  public enum SortDirection { Ascending, Descending }
  public record ScoresFilter(int? MinScore, int? MaxScore, string? Exchange);
  ```
- Create `dotnet/Stocks.DataModels/Scoring/LatestPrice.cs` — record for batch price lookup:
  ```csharp
  public record LatestPrice(string Ticker, decimal Close, DateOnly PriceDate);
  ```
- Create `dotnet/Stocks.DataModels/Scoring/BatchScoringConceptValue.cs` — extends scoring concept data with company identity:
  ```csharp
  public record BatchScoringConceptValue(ulong CompanyId, string ConceptName, decimal Value, DateOnly ReportDate, int BalanceTypeId);
  ```

**Test:** No unit tests — this checkpoint is purely schema migration and record type definitions (configuration).

**Verify:** `dotnet build dotnet/EDGARScraper.sln` compiles warning-free. Existing tests still pass: `dotnet test dotnet/EDGARScraper.sln`.

---

### Checkpoint 2: Batch Data Fetch Statements

**Build:**
- Create `dotnet/Stocks.Persistence/Database/Statements/GetAllScoringDataPointsStmt.cs`:
  - Adapts the SQL from research section 4A (CTE with `ROW_NUMBER() OVER (PARTITION BY company_id)`, `DISTINCT ON` deduplication, 5 most recent 10-K years per company)
  - Returns `(company_id, concept_name, value, report_date, balance_type_id)` for ALL companies
  - Inherits from `QueryDbStmtBase`
  - Takes `string[] conceptNames` as constructor parameter
  - Result type: `List<BatchScoringConceptValue>` (defined in checkpoint 1)

- Create `dotnet/Stocks.Persistence/Database/Statements/GetAllLatestPricesStmt.cs`:
  - SQL: `SELECT DISTINCT ON (ticker) ticker, close, price_date FROM prices ORDER BY ticker, price_date DESC`
  - Inherits from `QueryDbStmtBase`
  - Returns `List<LatestPrice>`

- Add to `IDbmService`:
  ```csharp
  Task<Result<IReadOnlyCollection<BatchScoringConceptValue>>> GetAllScoringDataPoints(
      string[] conceptNames, CancellationToken ct);
  Task<Result<IReadOnlyCollection<LatestPrice>>> GetAllLatestPrices(CancellationToken ct);
  ```

- Implement in `DbmService` — execute statements via `PostgresExecutor`
- Implement in `DbmInMemoryService` — filter `DbmInMemoryData` collections:
  - `GetAllScoringDataPoints`: iterate in-memory data points, filter by concept names, group by company, limit to 5 most recent 10-K years per company
  - `GetAllLatestPrices`: iterate in-memory prices, find latest per ticker

**Test:** In `dotnet/Stocks.EDGARScraper.Tests/`, create `BatchScoringDataFetchTests.cs`:
- Test `GetAllScoringDataPoints` via `DbmInMemoryService`:
  - Seed 2-3 companies with varying numbers of 10-K submissions and data points
  - Verify only 10-K data is returned (not 10-Q)
  - Verify only the 5 most recent years per company are returned
  - Verify concept name filtering works
  - Verify a company with no 10-K data returns no rows
- Test `GetAllLatestPrices` via `DbmInMemoryService`:
  - Seed prices for 2 tickers with multiple dates
  - Verify only the latest price per ticker is returned

**Verify:** `dotnet build dotnet/EDGARScraper.sln` compiles warning-free. `dotnet test dotnet/EDGARScraper.sln` — all tests pass.

---

### Checkpoint 3: Score Persistence, Batch Scoring Service, and CLI Command

**Build:**
- Create `dotnet/Stocks.Persistence/Database/Statements/TruncateCompanyScoresStmt.cs`:
  - SQL: `TRUNCATE TABLE company_scores`
  - Inherits from `NonQueryDbStmtBase`

- Create `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyScoresStmt.cs`:
  - Uses `COPY company_scores (...) FROM STDIN (FORMAT BINARY)` pattern
  - Inherits from `BulkInsertDbStmtBase<CompanyScoreSummary>`
  - Writes all columns from `CompanyScoreSummary`

- Add to `IDbmService`:
  ```csharp
  Task<Result> TruncateCompanyScores(CancellationToken ct);
  Task<Result> BulkInsertCompanyScores(List<CompanyScoreSummary> scores, CancellationToken ct);
  ```

- Implement in `DbmService` and `DbmInMemoryService`

- Add `ComputeAllScores` method to `ScoringService`:
  ```csharp
  public async Task<Result<IReadOnlyCollection<CompanyScoreSummary>>> ComputeAllScores(CancellationToken ct)
  ```
  Implementation:
  1. Call `_dbmService.GetAllScoringDataPoints(AllConceptNames, ct)` — single query for all companies
  2. Call `_dbmService.GetAllLatestPrices(ct)` — single query for all tickers
  3. Call `_dbmService.GetAllCompanyTickers(ct)` — existing method, maps company_id → ticker
  4. Call `_dbmService.GetAllCompanyNames(ct)` — existing method, maps company_id → name
  5. Call `_dbmService.GetAllCompaniesByDataSource("sec-edgar", ct)` — existing method, build company_id → Company lookup (provides CIK)
  6. Group batch scoring data by `CompanyId` into per-company `List<ScoringConceptValue>` (strip CompanyId, convert to existing type)
  7. Build company_id→ticker and ticker→LatestPrice lookup dictionaries
  8. For each company_id group:
     - Call `GroupByYear(values, out balanceTypes)` (existing static method)
     - Resolve shares outstanding from most recent year
     - Look up ticker, then latest price
     - Look up CIK from company lookup, name from company names
     - Call `ComputeDerivedMetrics(rawDataByYear, pricePerShare, sharesOutstanding, balanceTypes)` (existing static method)
     - Call `EvaluateChecks(metrics, yearsOfData)` (existing static method)
     - Count overall score and computable checks
     - Build `CompanyScoreSummary` record
  9. Return the collection
  Note: the method computes scores but does NOT write them — the caller (CLI command) handles truncate + insert.

- Add `--compute-all-scores` CLI command in `Program.cs`:
  1. Call `scoringService.ComputeAllScores(ct)`
  2. Call `_dbmService.TruncateCompanyScores(ct)`
  3. Call `_dbmService.BulkInsertCompanyScores(scores, ct)` (batch if >1000)
  4. Log count of scores computed

**Test:** In `dotnet/Stocks.EDGARScraper.Tests/`, create `BatchScoringServiceTests.cs`:
- Test `ComputeAllScores` via `DbmInMemoryService`:
  - Seed 2-3 companies with complete scoring data (data points, tickers, prices, company names)
  - Verify correct number of `CompanyScoreSummary` records returned
  - Verify overall scores match what `ComputeScore` would produce for each company individually (cross-check against existing per-company method)
  - Verify company with no 10-K data produces no score
  - Verify company with no price data still gets a score (with null market-cap-dependent fields)
- Test truncate + bulk insert round-trip via `DbmInMemoryService`:
  - Insert scores, verify they're in the in-memory store
  - Truncate, verify empty

**Verify:** `dotnet build dotnet/EDGARScraper.sln` compiles warning-free. `dotnet test dotnet/EDGARScraper.sln` — all tests pass.

---

### Checkpoint 4: Report Query Statement and API Endpoint

**Build:**
- Create `dotnet/Stocks.Persistence/Database/Statements/GetCompanyScoresStmt.cs`:
  - Paginated query on `company_scores` table
  - Dynamic `ORDER BY` based on `ScoresSortBy` enum (map enum values to column names; use a switch, NOT string interpolation for SQL injection safety)
  - Dynamic `WHERE` clauses based on `ScoresFilter` (min/max score, exchange)
  - Uses `COUNT(*) OVER()` for total count (same pattern as `SearchCompaniesStmt`)
  - Uses `LIMIT @limit OFFSET @offset` for pagination
  - Returns `List<CompanyScoreSummary>` + `PaginationResponse`
  - Inherits from `QueryDbStmtBase`

- Add to `IDbmService`:
  ```csharp
  Task<Result<PagedResults<CompanyScoreSummary>>> GetCompanyScores(
      PaginationRequest pagination, ScoresSortBy sortBy, SortDirection sortDir,
      ScoresFilter? filter, CancellationToken ct);
  ```

- Implement in `DbmService` and `DbmInMemoryService`

- Create `dotnet/Stocks.WebApi/Endpoints/ReportEndpoints.cs`:
  - `GET /api/reports/scores` endpoint
  - Query parameters: `page` (uint, default 1), `pageSize` (uint, default 50), `sortBy` (string, default "overallScore"), `sortDir` (string, default "desc"), `minScore` (int?), `maxScore` (int?), `exchange` (string?)
  - Parse `sortBy` string to `ScoresSortBy` enum, `sortDir` to `SortDirection` enum
  - Call `dbmService.GetCompanyScores(...)` and return paginated JSON response
  - Register in `Program.cs` or wherever endpoints are mapped (follow existing `MapScoringEndpoints` pattern)

**Test:** In `dotnet/Stocks.EDGARScraper.Tests/`, create `CompanyScoresReportTests.cs`:
- Test `GetCompanyScores` via `DbmInMemoryService`:
  - Seed `DbmInMemoryData` with pre-built `CompanyScoreSummary` records (5-10 companies with varying scores)
  - Test pagination: page 1 of 3, page 2 of 3, etc.
  - Test sorting: by overall score desc (top), by overall score asc (bottom), by estimated return CF
  - Test filtering: minScore=10 returns only high-scoring companies, exchange="NYSE" filters correctly
  - Test combined sort + filter
  - Test empty result (filter that matches nothing)

**Verify:** `dotnet build dotnet/EDGARScraper.sln` compiles warning-free. `dotnet test dotnet/EDGARScraper.sln` — all tests pass.

---

### Checkpoint 5: Angular Frontend — Scores Report Page

**Build:**
- Create `frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.ts`:
  - Standalone Angular component using signals with inline template and styles (matches existing `scoring.component.ts` pattern)
- Add route `/scores` in the app routing configuration
- Add navigation link to the scores report page (in the layout/nav component)
- API service method in `frontend/stocks-frontend/src/app/core/services/api.service.ts`:
  ```typescript
  getScoresReport(params: ScoresReportParams): Observable<PagedScoresResponse>
  ```
  - TypeScript interfaces for `CompanyScoreSummary`, `ScoresReportParams`, `PagedScoresResponse`
- Component features:
  - Sortable table headers (click to sort by column, toggle asc/desc)
  - Score badge with color coding (green ≥10, yellow ≥7, red <7) — reuse pattern from existing scoring component
  - Pagination controls (previous/next, page number display, page size selector)
  - Filter controls: min score dropdown, exchange dropdown
  - Each company name/row links to the per-company scoring detail page (`/company/{cik}/scoring`)
  - Display columns: Score, Company Name, Ticker, Exchange, Book Value, Market Cap, P/B, D/E, Est. Return (CF), Est. Return (OE)
  - Format numbers: currency for BookValue/MarketCap, ratio for D/E/P/B, percentage for estimated returns
  - Show `computed_at` timestamp somewhere (e.g., footer) so user knows data freshness

**Test:** No unit tests — this is a frontend UI checkpoint. Verify manually by running `ng serve` and navigating to `/scores`.

**Verify:** `cd frontend/stocks-frontend && ng build` compiles without errors. Manual verification: navigate to `http://localhost:4201/scores`, confirm table renders, sorting works, pagination works, score links navigate to company detail.

---

## Metadata
### Status
success
### Dependencies
- `ScoringService.cs` static methods (GroupByYear, ComputeDerivedMetrics, EvaluateChecks) must remain `internal static`
- V012 migration (`AddDataPointUpsertIndex`) must already be applied
- Existing `GetAllCompanyTickers`, `GetAllCompanyNames`, `GetAllCompaniesByDataSource` methods in `IDbmService`
- Price data and 10-K filing data must already be imported before running `--compute-all-scores`
### Open Questions
- Whether the batch scoring data fetch query performs acceptably on real data (will test with EXPLAIN ANALYZE during checkpoint 2 implementation)
- Optimal batch size for in-memory processing if memory pressure is an issue (start unbatched, optimize if needed)
### Assumptions
- ~8,000-10,000 companies with 10-K filings
- Scores are rebuilt from scratch each time (truncate + insert), not incrementally updated
- The existing Angular workspace at `frontend/stocks-frontend/` has routing and layout already configured
- The `ScoringService` is already registered in DI for both the CLI app and WebApi
