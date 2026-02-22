# Research: Investment Return Report for Graham/Buffett Score Pages

## Table of Contents

1. [What Does "Day X" Mean Operationally?](#1-what-does-day-x-mean-operationally)
2. [What Price History Depth Exists?](#2-what-price-history-depth-exists)
3. [How Are List-Page Results Filtered by Score?](#3-how-are-list-page-results-filtered-by-score)
4. [What Backend Response DTOs Exist?](#4-what-backend-response-dtos-exist)
5. [How Does the Detail Page Link from the List Page?](#5-how-does-the-detail-page-link-from-the-list-page)
6. [Is There Any Existing ROI Calculation Logic?](#6-is-there-any-existing-roi-calculation-logic)
7. [What Is the Formula for Annualized Return?](#7-what-is-the-formula-for-annualized-return)
8. [How Does the Batch Score Computation Pipeline Work?](#8-how-does-the-batch-score-computation-pipeline-work)
9. [Existing Patterns to Follow](#9-existing-patterns-to-follow)
10. [Risks and Concerns](#10-risks-and-concerns)
11. [Recommended Approach](#11-recommended-approach)

---

## 1. What Does "Day X" Mean Operationally?

**No historical score snapshots exist.** Both `company_scores` and `company_moat_scores` tables store a single row per company (keyed by `company_id`). The batch pipeline (`--compute-all-scores` / `--compute-all-moat-scores`) truncates the entire table and bulk-inserts fresh rows each run. The `computed_at` column records when the latest batch ran, not when a company first achieved a particular score.

**Score table schemas (relevant columns):**

| Table | Key | Timestamp | Price Snapshot |
|---|---|---|---|
| `company_scores` | `company_id` (PK) | `computed_at timestamptz` | `price_per_share`, `price_date` |
| `company_moat_scores` | `company_id` (PK) | `computed_at timestamptz` | `price_per_share`, `price_date` |

**DDL sources:**
- `dotnet/Stocks.Persistence/Database/Migrations/V013__AddCompanyScoresTable.sql`
- `dotnet/Stocks.Persistence/Database/Migrations/V017__AddCompanyMoatScoresTable.sql`

**Implication for "day X":** Since there is no history of when a company first achieved a score threshold, `computed_at` cannot serve as a meaningful "buy date." The most practical definition of "day X" is either:
- A **user-selected date** (e.g., date picker defaulting to `computed_at` or 1 year ago)
- The **earliest available price date** for that ticker in the `prices` table
- A **fixed lookback** (e.g., "since 1 year ago", "since 5 years ago")

The `prices` table (`dotnet/Stocks.Persistence/Database/Migrations/V007__AddPrices.sql`) stores daily OHLCV data with a unique constraint on `(ticker, price_date)`, so the earliest available price for any ticker is deterministic.

---

## 2. What Price History Depth Exists?

**Source:** Stooq (free market data provider), daily frequency.

**Import pipeline:**
1. **Single-ticker import** (`--import-prices-stooq`): Reads individual CSV files from a configured directory. Processes up to `MaxTickersPerRun` (default 200) tickers per run, prioritizing least-recently imported tickers via the `price_imports` tracking table.
2. **Bulk import** (`--import-prices-stooq-bulk`): Reads from pre-downloaded Stooq `.txt` files for one-time historical backfills.

**Storage pattern:** Delete-then-insert per ticker — all existing prices for a ticker are removed, then new prices are bulk-inserted using PostgreSQL `COPY` in binary format (batches of 500 rows).

**Date range:** Depends entirely on what was downloaded from Stooq. Stooq provides multi-year daily history for major US tickers (typically 20+ years for large-cap). The system does not impose any date range limits.

**Tracking tables:**
- `price_imports` (`cik, ticker, exchange, last_imported_utc`) — tracks last import timestamp per ticker
- `price_downloads` (`cik, ticker, exchange, last_downloaded_utc`) — tracks last download timestamp per ticker

**Key files:**
- `dotnet/Stocks.EDGARScraper/Services/StooqPriceImporter.cs` — single-ticker CSV import
- `dotnet/Stocks.EDGARScraper/Services/StooqBulkPriceImporter.cs` — bulk import
- `dotnet/Stocks.Persistence/Database/Statements/BulkInsertPricesStmt.cs` — PostgreSQL COPY-based bulk insert

**PriceRow model** (`dotnet/Stocks.DataModels/PriceRow.cs`):
```csharp
public record PriceRow(
    ulong PriceId, ulong Cik, string Ticker, string? Exchange,
    string StooqSymbol, DateOnly PriceDate, decimal Open, decimal High,
    decimal Low, decimal Close, long Volume);
```

---

## 3. How Are List-Page Results Filtered by Score?

Both `GetCompanyScoresStmt.cs` and `GetCompanyMoatScoresStmt.cs` use parameterized WHERE clauses:

```sql
WHERE overall_score >= @min_score
  AND overall_score <= @max_score
  AND exchange = @exchange
```

Each filter is optional and independently applied. Pagination uses `LIMIT` + `OFFSET` with `COUNT(*) OVER()` to get total count in one query.

**Extending for investment return data:** The queries read from the pre-computed `company_scores` / `company_moat_scores` tables. Adding investment return data to the list page would require either:
- **Option A:** Pre-compute and store return values in the score tables (add columns, compute during batch pipeline) — best for list-page performance
- **Option B:** JOIN with the `prices` table in the query — complex, would require price lookups for each row's `computed_at` date vs latest price
- **Option C:** Enrich results after the query in the endpoint handler — fetch prices separately and compute returns in-memory

**Key files:**
- `dotnet/Stocks.Persistence/Database/Statements/GetCompanyScoresStmt.cs`
- `dotnet/Stocks.Persistence/Database/Statements/GetCompanyMoatScoresStmt.cs`

---

## 4. What Backend Response DTOs Exist?

### 4.1. What Backend Response DTOs Exist - List Page DTOs

**CompanyScoreSummary** (Graham list, `dotnet/Stocks.DataModels/Scoring/CompanyScoreSummary.cs`):
- Identity: `CompanyId`, `Cik`, `CompanyName`, `Ticker`, `Exchange`
- Score: `OverallScore`, `ComputableChecks`, `YearsOfData`
- Metrics: `BookValue`, `MarketCap`, `DebtToEquityRatio`, `PriceToBookRatio`, `DebtToBookRatio`, `AdjustedRetainedEarnings`, `AverageNetCashFlow`, `AverageOwnerEarnings`, `AverageRoeCF`, `AverageRoeOE`, `EstimatedReturnCF`, `EstimatedReturnOE`
- Valuation: `PricePerShare`, `PriceDate`, `SharesOutstanding`, `CurrentDividendsPaid`, `MaxBuyPrice`, `PercentageUpside`
- Metadata: `ComputedAt`

**CompanyMoatScoreSummary** (Buffett list, `dotnet/Stocks.DataModels/Scoring/CompanyMoatScoreSummary.cs`):
- Identity: `CompanyId`, `Cik`, `CompanyName`, `Ticker`, `Exchange`
- Score: `OverallScore`, `ComputableChecks`, `YearsOfData`
- Metrics: `AverageGrossMargin`, `AverageOperatingMargin`, `AverageRoeCF`, `AverageRoeOE`, `EstimatedReturnOE`, `RevenueCagr`, `CapexRatio`, `InterestCoverage`, `DebtToEquityRatio`
- Valuation: `PricePerShare`, `PriceDate`, `SharesOutstanding`
- Metadata: `ComputedAt`

### 4.2. What Backend Response DTOs Exist - Detail Page DTOs

**ScoringResult** (Graham detail, `dotnet/Stocks.DataModels/Scoring/ScoringResult.cs`):
- `RawDataByYear`, `Metrics` (DerivedMetrics), `Scorecard` (ScoringCheck[]), `OverallScore`, `ComputableChecks`, `YearsOfData`, `PricePerShare`, `PriceDate`, `SharesOutstanding`, `MaxBuyPrice`, `PercentageUpside`

**MoatScoringResult** (Buffett detail, `dotnet/Stocks.DataModels/Scoring/MoatScoringResult.cs`):
- `RawDataByYear`, `Metrics` (MoatDerivedMetrics), `Scorecard` (ScoringCheck[]), `TrendData` (MoatYearMetrics[]), `OverallScore`, `ComputableChecks`, `YearsOfData`, `PricePerShare`, `PriceDate`, `SharesOutstanding`

**New fields needed for investment return:** An investment return section on detail pages would need new fields not present in current DTOs (e.g., `InvestmentReturnSinceComputedAt`, `AnnualizedReturn`, `PriceAtComputedAt`). These would come from a new endpoint or be added to the existing scoring response.

---

## 5. How Does the Detail Page Link from the List Page?

**Graham:** List page links via `<a [routerLink]="['/company', row.cik, 'scoring']" target="_blank">` → route `/company/:cik/scoring` → `ScoringComponent`

**Buffett:** List page links via `<a [routerLink]="['/company', row.cik, 'moat-scoring']" target="_blank">` → route `/company/:cik/moat-scoring` → `MoatScoringComponent`

Both links open in a new tab.

**Routing structure** (`frontend/stocks-frontend/src/app/app.routes.ts`):
```
/scores                     → ScoresReportComponent (Graham list)
/moat-scores                → MoatScoresReportComponent (Buffett list)
/company/:cik/scoring       → ScoringComponent (Graham detail)
/company/:cik/moat-scoring  → MoatScoringComponent (Buffett detail)
```

**Integration point for investment return:** A new section can be added directly to the existing detail page components (Graham `ScoringComponent` and Buffett `MoatScoringComponent`), positioned after the Derived Metrics table and before the trend/raw data sections. No routing changes needed unless a standalone investment return page is desired.

---

## 6. Is There Any Existing ROI Calculation Logic?

**No actual historical investment return calculation exists.** The codebase has two related but different concepts:

### 6.1. Is There Any Existing ROI Calculation Logic - Revenue CAGR (MoatScoringService)

**Location:** `dotnet/Stocks.Persistence/Services/MoatScoringService.cs` (lines 257-267)

```csharp
double ratio = (double)(latestRevenue.Value / oldestRevenue.Value);
double cagrDouble = Math.Pow(ratio, 1.0 / yearsBetween) - 1.0;
revenueCagr = (decimal)cagrDouble * 100m;
```

This calculates revenue growth rate, not investment return. But the CAGR formula pattern is reusable.

### 6.2. Is There Any Existing ROI Calculation Logic - Estimated Return (Earning Yield)

**Location:** `dotnet/Stocks.Persistence/Services/ScoringService.cs` (lines 928-934)

```csharp
estimatedReturnCF = 100m * (averageNetCashFlow.Value - currentDividendsPaid.Value) / marketCap.Value;
estimatedReturnOE = 100m * (averageOwnerEarnings.Value - currentDividendsPaid.Value) / marketCap.Value;
```

This is a **forward-looking earning yield** (retained earnings / market cap), not a historical price-based return. The same formula appears in `MoatScoringService.cs` for the OE variant only.

**Conclusion:** The investment return calculation ("If I invested $1000 on day X, how much would I have today?") is entirely new. The CAGR formula from `MoatScoringService` can serve as a pattern for the annualized return calculation.

---

## 7. What Is the Formula for Annualized Return?

**Standard formula:**
```
AnnualizedReturn = ((EndPrice / StartPrice) ^ (365.25 / DaysHeld)) - 1
```

**For the "$1000 invested" scenario:**
```
SharesBought = 1000 / StartPrice
CurrentValue = SharesBought * EndPrice = 1000 * (EndPrice / StartPrice)
TotalReturn = (CurrentValue / 1000) - 1 = (EndPrice / StartPrice) - 1
AnnualizedReturn = ((EndPrice / StartPrice) ^ (365.25 / DaysHeld)) - 1
```

**Existing CAGR pattern to follow** (from `MoatScoringService`):
```csharp
double ratio = (double)(endPrice / startPrice);
double annualizedReturn = Math.Pow(ratio, 365.25 / daysHeld) - 1.0;
```

The existing code handles edge cases: checks `double.IsFinite()`, guards against division by zero, and clamps extreme values before converting to decimal. These same guards should be applied to the investment return calculation.

---

## 8. How Does the Batch Score Computation Pipeline Work?

**CLI commands:** `--compute-all-scores` (Graham) and `--compute-all-moat-scores` (Buffett), wired in `dotnet/Stocks.EDGARScraper/Program.cs` (lines 979-1029).

**Pipeline flow:**
1. Pre-fetch all data: scoring concept values (8-year window), latest prices, company tickers, company names
2. Compute scores for all companies in memory
3. Truncate the target table (`company_scores` or `company_moat_scores`)
4. Bulk-insert all computed `CompanyScoreSummary` / `CompanyMoatScoreSummary` records

**Key characteristics:**
- Entire table is truncated and rebuilt each run (no incremental updates)
- Pre-fetches ALL companies' data upfront for batch efficiency
- Individual scores can also be computed on-demand via detail endpoints

**Should investment return be pre-computed in batch?**

**Recommendation: Compute on-the-fly**, not in batch, because:
1. The "start date" is user-selectable (not a fixed value) — can't pre-compute all possible date ranges
2. Price data changes daily — pre-computed returns would be stale immediately
3. The calculation is cheap: one price lookup + one division + one `Math.Pow` call
4. For the list page, a fixed lookback (e.g., "return since `computed_at`" or "1-year return") could be pre-computed if needed for sort/filter performance

---

## 9. Existing Patterns to Follow

### 9.1. Existing Patterns to Follow - Backend

- **Statement pattern:** Create a new `GetPriceOnDateStmt` (or reuse `GetPricesByTickerStmt` and filter in-memory) to fetch the closing price on or near a given date
- **Result pattern:** Return `Result<InvestmentReturn>` with proper error codes for missing data (no prices, ticker not found, etc.)
- **Endpoint pattern:** Follow `ScoringEndpoints.cs` / `MoatScoringEndpoints.cs` for routing and parameter binding
- **CAGR pattern:** Follow `MoatScoringService.cs` lines 257-267 for `Math.Pow` with overflow guards

### 9.2. Existing Patterns to Follow - Frontend

- **Signal pattern:** Use Angular signals (`signal<T>()`, `computed()`) as in `ScoringComponent` and `MoatScoringComponent`
- **Sparkline pattern:** Use the existing `computeSparkline()` utility from the Buffett detail page for trend charts
- **Table pattern:** Use the existing derived metrics table styling (max-width 500px, label/value columns)
- **API service pattern:** Add methods to `api.service.ts` following existing `getScoring(cik)` / `getMoatScoring(cik)` patterns

---

## 10. Risks and Concerns

1. **Missing price data:** Not all scored companies may have price history in the `prices` table. The endpoint must handle this gracefully (return null/N/A rather than error).
2. **Price date alignment:** A requested start date may fall on a weekend/holiday. The query should find the nearest available trading day (closest price_date ≤ requested date, or ≥ if no prior date exists).
3. **Stock splits/dividends:** Stooq data may or may not be split-adjusted. If not, returns could be misleading for companies that split during the period. This is a data quality concern worth noting in the UI.
4. **Ticker changes:** Some companies change tickers over time. The `prices` table keys on `ticker`, so historical prices under a previous ticker would not be found. The score tables store the current ticker.
5. **List page performance:** Adding per-company return calculations to the list page (50-100 rows) could be expensive if computed on-the-fly with individual price lookups. Pre-computation or batch enrichment may be needed for list pages.
6. **"Day X" ambiguity for aggregate reports:** On the list page, showing "return since computed_at" only works if `computed_at` is the same for all companies (which it is, since batch runs truncate and reinsert all at once). But `computed_at` may be very recent (e.g., yesterday), making the return trivially near zero.

---

## 11. Recommended Approach

### 11.1. Recommended Approach - Detail Pages (Per-Company)

1. **New backend endpoint:** `GET /api/companies/{cik}/investment-return?startDate=YYYY-MM-DD`
   - Accepts optional `startDate` (defaults to 1 year ago)
   - Looks up ticker for the CIK
   - Fetches the closing price on/near `startDate` and the latest closing price
   - Computes total return, annualized return, and "$1000 invested" value
   - Returns a DTO with start price, end price, start date, end date, total return %, annualized return %, and current value of $1000
2. **New frontend section:** Add an "Investment Return" card to both `ScoringComponent` and `MoatScoringComponent`, positioned after Derived Metrics
   - Date picker for start date (default: 1 year ago or `computedAt`)
   - Display: "$1,000 invested on {date} → ${value} today ({return}% total, {annualized}% annualized)"
   - Optional: sparkline of price history over the period

### 11.2. Recommended Approach - List Pages (Aggregate)

1. **Pre-compute a fixed-period return** during the batch scoring pipeline (e.g., 1-year return as of `computed_at`)
   - Add columns `return_1y` to `company_scores` and `company_moat_scores` tables
   - Compute during `--compute-all-scores` / `--compute-all-moat-scores` using the existing pre-fetched price data
   - Add as a sortable column in the list page
2. **Alternatively:** Enrich list results in the endpoint handler by fetching prices for all tickers on the page (batch query) and computing returns in memory — avoids schema changes but adds latency

### 11.3. Recommended Approach - Implementation Order

1. Detail page endpoint + DTO (backend)
2. Detail page UI section (frontend)
3. List page pre-computed column (backend schema + batch pipeline)
4. List page UI column (frontend)

---

## Metadata

### Status
success

### Dependencies
- Price data must be imported for scored companies (via `--import-prices-stooq` or `--import-prices-stooq-bulk`)
- Scores must be pre-computed (via `--compute-all-scores` / `--compute-all-moat-scores`)

### Open Questions
- Should Stooq price data be treated as split-adjusted? (Affects return accuracy for companies that have split)
- What fixed lookback periods should the list page show? (1 year, 3 years, 5 years, since `computed_at`?)
- Should the detail page support multiple lookback periods or a free-form date picker?

### Assumptions
- Stooq daily close prices are sufficient for return calculations (no need for intraday or adjusted close)
- The `computed_at` date from the scores tables is the same for all companies in a given batch run (truncate + bulk insert)
- "Day X" on the detail page will default to a reasonable lookback (e.g., 1 year ago) rather than requiring user input

<!-- Self-review: converged after 2 passes -->
