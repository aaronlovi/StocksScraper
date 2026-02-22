# Plan: Investment Return Report for Graham/Buffett Score Pages

## Table of Contents

1. [Overview](#1-overview)
2. [Checkpoint 1: Backend — Detail Page Investment Return Service + Endpoint](#2-checkpoint-1)
3. [Checkpoint 2: Frontend — Detail Page Investment Return Section](#3-checkpoint-2)
4. [Checkpoint 3: Backend — List Page Pre-Computed 1-Year Return](#4-checkpoint-3)
5. [Checkpoint 4: Frontend — List Page Return Column](#5-checkpoint-4)

---

## 1. Overview

Add "If I invested $1,000 on day X, how much would I have today?" reporting to both Graham (Value Score) and Buffett (Moat Score) pages.

**Detail pages**: On-the-fly calculation via a new endpoint. User selects a start date (default: 1 year ago). Shows total return %, annualized return %, and current value of $1,000.

**List pages**: Pre-computed 1-year return column added during the batch scoring pipeline. Sortable in the list table.

---

## 2. Checkpoint 1: Backend — Detail Page Investment Return Service + Endpoint

### 2.1. Checkpoint 1 - Build

#### 2.1.1. Checkpoint 1 - Build - New DTO: `InvestmentReturnResult`

Create `dotnet/Stocks.DataModels/Scoring/InvestmentReturnResult.cs`:

```csharp
public record InvestmentReturnResult(
    string Ticker,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal StartPrice,
    decimal EndPrice,
    decimal TotalReturnPct,
    decimal? AnnualizedReturnPct,
    decimal CurrentValueOf1000
);
```

- `TotalReturnPct`: `(EndPrice / StartPrice - 1) * 100`
- `AnnualizedReturnPct`: `(Math.Pow(EndPrice / StartPrice, 365.25 / daysHeld) - 1) * 100`. Null if held less than 1 day.
- `CurrentValueOf1000`: `1000 * EndPrice / StartPrice`

#### 2.1.2. Checkpoint 1 - Build - New Statement: `GetPriceNearDateStmt`

Create `dotnet/Stocks.Persistence/Database/Statements/GetPriceNearDateStmt.cs`.

Finds the closest trading-day price on or before a target date for a given ticker:

```sql
SELECT price_id, cik, ticker, exchange, stooq_symbol, price_date, open, high, low, close, volume
FROM prices
WHERE ticker = @ticker AND price_date <= @target_date
ORDER BY price_date DESC
LIMIT 1
```

Follow the `QueryDbStmtBase` pattern from `GetPricesByTickerStmt`: static column index fields, `BeforeRowProcessing` for ordinal caching, `ProcessCurrentRow` to build a `PriceRow`. Expose result via a `Price` property (`PriceRow?`, null if no rows matched).

#### 2.1.3. Checkpoint 1 - Build - New Statement: `GetLatestPriceByTickerStmt`

Create `dotnet/Stocks.Persistence/Database/Statements/GetLatestPriceByTickerStmt.cs`.

Gets the most recent price for a ticker:

```sql
SELECT price_id, cik, ticker, exchange, stooq_symbol, price_date, open, high, low, close, volume
FROM prices
WHERE ticker = @ticker
ORDER BY price_date DESC
LIMIT 1
```

Same pattern as `GetPriceNearDateStmt` — separate class because the parameter set differs (no `@target_date`). Expose via `Price` property (`PriceRow?`).

#### 2.1.4. Checkpoint 1 - Build - IDbmService + Implementations

Add two methods to `IDbmService`:

```csharp
Task<Result<PriceRow?>> GetPriceNearDate(string ticker, DateOnly targetDate, CancellationToken ct);
Task<Result<PriceRow?>> GetLatestPriceByTicker(string ticker, CancellationToken ct);
```

Implement in `DbmService` (execute the statements via `PostgresExecutor`) and `DbmInMemoryService` (filter `DbmInMemoryData.Prices` in-memory: find prices matching ticker, filter by `PriceDate <= targetDate`, sort descending, take first).

#### 2.1.5. Checkpoint 1 - Build - New Service: `InvestmentReturnService`

Create `dotnet/Stocks.Persistence/Services/InvestmentReturnService.cs`.

```csharp
public class InvestmentReturnService
{
    private readonly IDbmService _dbm;

    public async Task<Result<InvestmentReturnResult>> ComputeReturn(
        string ticker, DateOnly startDate, CancellationToken ct)
    {
        // 1. Fetch start price via GetPriceNearDate(ticker, startDate)
        // 2. Fetch end price via GetLatestPriceByTicker(ticker)
        // 3. Guard: if either is null, return Result.Failure with NoPriceData error
        // 4. Guard: if startPrice.Close <= 0, return failure
        // 5. Compute totalReturnPct, annualizedReturnPct, currentValueOf1000
        // 6. Use CAGR pattern from MoatScoringService: Math.Pow with double.IsFinite guard
        // 7. Return Result.Success(new InvestmentReturnResult(...))
    }
}
```

Follow the CAGR overflow protection pattern from `MoatScoringService` lines 258-267:
```csharp
double ratio = (double)(endPrice / startPrice);
int daysHeld = endDate.DayNumber - startDate.DayNumber;
if (daysHeld < 1) annualizedReturnPct = null;
else {
    double annualized = Math.Pow(ratio, 365.25 / daysHeld) - 1.0;
    if (double.IsFinite(annualized) && Math.Abs(annualized) < (double)decimal.MaxValue)
        annualizedReturnPct = (decimal)annualized * 100m;
    else
        annualizedReturnPct = null;
}
```

#### 2.1.6. Checkpoint 1 - Build - New ErrorCode

Add `NoPriceData` to `dotnet/Stocks.Shared/ErrorCodes.cs` if it does not already exist. Used when no price is found for the ticker or date range.

#### 2.1.7. Checkpoint 1 - Build - New Endpoint

Create `dotnet/Stocks.WebApi/Endpoints/InvestmentReturnEndpoints.cs`.

Map `GET /api/companies/{cik}/investment-return`:
- Query param: `startDate` (optional, format `yyyy-MM-dd`, default: 1 year before today)
- Dependency injection: `IDbmService`, `CancellationToken`
- Flow:
  1. `dbm.GetCompanyByCik(cik)` → get company
  2. `dbm.GetTickerForCompany(companyId)` (or equivalent) → get ticker
  3. Parse `startDate` or default to `DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1)` (or equivalent)
  4. `new InvestmentReturnService(dbm).ComputeReturn(ticker, startDate, ct)`
  5. Return `Results.Ok(anonymous object)` with camelCase properties

Wire the endpoint in the WebApi startup (follow pattern of existing `MapScoringEndpoints()` / `MapMoatScoringEndpoints()`).

### 2.2. Checkpoint 1 - Test

Add test class `InvestmentReturnServiceTests.cs` in `dotnet/Stocks.EDGARScraper.Tests/Scoring/`:

1. **Basic return calculation**: Seed company + prices (start: $100, end: $150). Assert total return = 50%, annualized correct, $1000 → $1500.
2. **Negative return**: Seed prices (start: $100, end: $80). Assert total return = -20%, $1000 → $800.
3. **Missing start price**: No price on or before start date. Assert `Result.IsFailure` with `NoPriceData`.
4. **Missing end price**: No prices at all for ticker. Assert `Result.IsFailure`.
5. **Same-day**: Start price date equals end price date (daysHeld = 0). Assert `AnnualizedReturnPct` is null, total return is 0%.
6. **Weekend alignment**: Start date is a Saturday, nearest trading day (Friday) price is used. Assert `StartDate` in result reflects the actual price date.
7. **Overflow protection**: Extreme price ratio (start: $0.01, end: $10000). Assert no exception; annualized may be null.

Use `DbmInMemoryService` and seed prices via `BulkInsertPrices`. Seed a company + ticker via existing seed helpers.

### 2.3. Checkpoint 1 - Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

## 3. Checkpoint 2: Frontend — Detail Page Investment Return Section

### 3.1. Checkpoint 2 - Build

#### 3.1.1. Checkpoint 2 - Build - API Service Method

Add to `frontend/stocks-frontend/src/app/core/services/api.service.ts`:

```typescript
getInvestmentReturn(cik: string, startDate?: string): Observable<InvestmentReturnResponse> {
    const parts: string[] = [];
    if (startDate) parts.push(`startDate=${startDate}`);
    const qs = parts.length ? `?${parts.join('&')}` : '';
    return this.http.get<InvestmentReturnResponse>(`${this.baseUrl}/api/companies/${cik}/investment-return${qs}`);
}
```

Add `InvestmentReturnResponse` interface (in the same file or a models file, matching existing pattern):

```typescript
export interface InvestmentReturnResponse {
    ticker: string;
    startDate: string;
    endDate: string;
    startPrice: number;
    endPrice: number;
    totalReturnPct: number;
    annualizedReturnPct: number | null;
    currentValueOf1000: number;
}
```

#### 3.1.2. Checkpoint 2 - Build - ScoringComponent Investment Return Section

Modify `frontend/stocks-frontend/src/app/features/scoring/scoring.component.ts` and its HTML template:

**Component state** (signals):
```typescript
investmentReturn = signal<InvestmentReturnResponse | null>(null);
investmentReturnLoading = signal(false);
investmentReturnError = signal<string | null>(null);
startDate = signal<string>(this.defaultStartDate());
```

Where `defaultStartDate()` returns a `yyyy-MM-dd` string for 1 year ago.

**On init**: Call `loadInvestmentReturn()` with default date.

**On date change**: Re-call `loadInvestmentReturn()` with new date.

**Template**: Add a section after the Derived Metrics table and before Raw Data. Style it like the existing metric tables (max-width ~500px, label/value rows). Content:

- Date input (`type="date"`) bound to `startDate` signal
- "Start Price" row: `$XX.XX on YYYY-MM-DD`
- "Current Price" row: `$XX.XX on YYYY-MM-DD`
- "Total Return" row: `+XX.XX%` (green if positive, red if negative)
- "Annualized Return" row: `+XX.XX%` (green/red, or "N/A" if null)
- "$1,000 Invested" row: `$X,XXX.XX`
- Loading/error states with `@if` blocks

Use existing formatting helpers (`fmtCurrency`, `fmtPct`) or add minimal new ones following the same pattern.

#### 3.1.3. Checkpoint 2 - Build - MoatScoringComponent Investment Return Section

Apply the same pattern to `frontend/stocks-frontend/src/app/features/moat-scoring/moat-scoring.component.ts` and its HTML template. The section is identical in both components. If code duplication is minimal (one section of template + 4 signals + 1 method), inline in both components. If substantial, extract a shared `investment-return` child component.

### 3.2. Checkpoint 2 - Test

Frontend has no existing test infrastructure beyond the Angular CLI defaults. This checkpoint's tests are covered by:
- Manual verification that the section renders with mock data
- Compilation check (`ng build` or equivalent if configured)
- Backend tests from Checkpoint 1 cover the API contract

If Angular tests exist in the project, add a minimal test for the API service method.

### 3.3. Checkpoint 2 - Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
cd frontend/stocks-frontend && npx ng build --configuration production 2>&1 | head -50
```

---

## 4. Checkpoint 3: Backend — List Page Pre-Computed 1-Year Return

### 4.1. Checkpoint 3 - Build

#### 4.1.1. Checkpoint 3 - Build - New Statement: `GetAllPricesNearDateStmt`

Create `dotnet/Stocks.Persistence/Database/Statements/GetAllPricesNearDateStmt.cs`.

Bulk-fetches the closest price on or before a target date for ALL tickers in one query:

```sql
SELECT DISTINCT ON (ticker) price_id, cik, ticker, exchange, stooq_symbol, price_date, open, high, low, close, volume
FROM prices
WHERE price_date <= @target_date
ORDER BY ticker, price_date DESC
```

Follow `QueryDbStmtBase` pattern. Return `IReadOnlyCollection<PriceRow>`. Add corresponding `GetAllPricesNearDate(DateOnly targetDate, CancellationToken ct)` method to `IDbmService`, `DbmService`, and `DbmInMemoryService`.

#### 4.1.2. Checkpoint 3 - Build - Migration: Add `return_1y` Columns

Create a new migration `dotnet/Stocks.Persistence/Database/Migrations/V0XX__AddReturn1yToScoreTables.sql` (use next available version number):

```sql
ALTER TABLE company_scores ADD COLUMN return_1y numeric(10,4);
ALTER TABLE company_moat_scores ADD COLUMN return_1y numeric(10,4);
```

Nullable column — null means no price data available for 1-year return.

#### 4.1.3. Checkpoint 3 - Build - Update DTOs

Add `decimal? Return1y` parameter to both:
- `CompanyScoreSummary` record (after `PercentageUpside`, before `ComputedAt`)
- `CompanyMoatScoreSummary` record (after `SharesOutstanding`, before `ComputedAt`)

#### 4.1.4. Checkpoint 3 - Build - Update BulkInsert Statements

Update `BulkInsertCompanyScoresStmt` and `BulkInsertCompanyMoatScoresStmt`:
- Add `return_1y` to the COPY column list
- Add `WriteNullableAsync(s.Return1y, NpgsqlDbType.Numeric)` in `WriteItemAsync`

#### 4.1.5. Checkpoint 3 - Build - Update Get/Query Statements

Update `GetCompanyScoresStmt` and `GetCompanyMoatScoresStmt`:
- Add `return_1y` to the SELECT column list
- Add static column index field and ordinal caching
- Read in `ProcessCurrentRow`: `reader.IsDBNull(_return1yIndex) ? null : reader.GetDecimal(_return1yIndex)`
- Pass the new field to the DTO constructor
- Add `Return1y` to the `ScoresSortBy` / `MoatScoresSortBy` enum (or equivalent sort column mapping) so list pages can sort by it

#### 4.1.6. Checkpoint 3 - Build - Update Batch Scoring Pipeline

In `ScoringService.ComputeAllScores()` and `MoatScoringService.ComputeAllMoatScores()` (or in `Program.cs` `ComputeAndStoreAllScoresAsync` / `ComputeAndStoreAllMoatScoresAsync`):

1. After computing scores but before bulk insert, fetch prices needed for 1-year return:
   - Call `GetAllLatestPricesStmt` (already fetched) — this gives current prices by ticker
   - Call new `GetAllPricesNearDateStmt` with `DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1)` — this gives prices from ~1 year ago by ticker
2. Build a lookup dictionary: `ticker → (latestPrice, yearAgoPrice)`
3. For each `CompanyScoreSummary`/`CompanyMoatScoreSummary`, compute `Return1y`:
   ```csharp
   if (latestPrice != null && yearAgoPrice != null && yearAgoPrice.Close > 0)
       return1y = ((latestPrice.Close / yearAgoPrice.Close) - 1m) * 100m;
   else
       return1y = null;
   ```
4. Since DTOs are records (immutable), use `with` expression to create a copy with `Return1y` set, or restructure DTO construction to include it from the start.

#### 4.1.7. Checkpoint 3 - Build - Update Report Endpoints

Update `ReportEndpoints.cs` and `MoatReportEndpoints.cs` to include `return1y` in the response object (it's already in the DTO, just ensure the anonymous response object maps it).

### 4.2. Checkpoint 3 - Test

Add/update tests in `dotnet/Stocks.EDGARScraper.Tests/`:

1. **Batch scoring with return**: Seed company, prices (1 year ago + today), run `ComputeAllScores`. Assert `Return1y` is computed correctly.
2. **Batch scoring without price data**: Seed company without prices. Assert `Return1y` is null.
3. **Batch scoring partial price data**: Seed company with only current price (no historical). Assert `Return1y` is null.
4. **GetAllPricesNearDateStmt**: Seed multiple tickers with prices. Assert correct price returned for each ticker at target date.

### 4.3. Checkpoint 3 - Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

## 5. Checkpoint 4: Frontend — List Page Return Column

### 5.1. Checkpoint 4 - Build

#### 5.1.1. Checkpoint 4 - Build - Update TypeScript Interfaces

Update `CompanyScoreSummary` and `CompanyMoatScoreSummary` interfaces (in `api.service.ts` or models file) to include `return1y: number | null`.

#### 5.1.2. Checkpoint 4 - Build - ScoresReportComponent

Modify `frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.ts` and HTML:

- Add a "1Y Return" column after the "Price" column (or after "% Upside")
- Make it sortable: add `'return1y'` to the sort column mapping
- Format: `+XX.XX%` or `-XX.XX%`, color green/red. Show "N/A" if null.
- Column header: `<th class="num sortable" (click)="toggleSort('return1y')">1Y Return</th>`

#### 5.1.3. Checkpoint 4 - Build - MoatScoresReportComponent

Apply the same pattern to `frontend/stocks-frontend/src/app/features/moat-scores-report/moat-scores-report.component.ts` and HTML:

- Add "1Y Return" column in the same relative position
- Same sorting, formatting, and color-coding logic

### 5.2. Checkpoint 4 - Test

- Compilation check (`ng build`)
- Backend sort functionality is already covered by existing list endpoint test patterns — the new sort column follows the same enum/switch pattern

### 5.3. Checkpoint 4 - Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
cd frontend/stocks-frontend && npx ng build --configuration production 2>&1 | head -50
```

---

## Files to Create

| File | Checkpoint |
|------|-----------|
| `dotnet/Stocks.DataModels/Scoring/InvestmentReturnResult.cs` | 1 |
| `dotnet/Stocks.Persistence/Database/Statements/GetPriceNearDateStmt.cs` | 1 |
| `dotnet/Stocks.Persistence/Database/Statements/GetLatestPriceByTickerStmt.cs` | 1 |
| `dotnet/Stocks.Persistence/Services/InvestmentReturnService.cs` | 1 |
| `dotnet/Stocks.WebApi/Endpoints/InvestmentReturnEndpoints.cs` | 1 |
| `dotnet/Stocks.EDGARScraper.Tests/Scoring/InvestmentReturnServiceTests.cs` | 1 |
| `dotnet/Stocks.Persistence/Database/Statements/GetAllPricesNearDateStmt.cs` | 3 |
| `dotnet/Stocks.Persistence/Database/Migrations/V0XX__AddReturn1yToScoreTables.sql` | 3 |

## Files to Modify

| File | Checkpoint |
|------|-----------|
| `dotnet/Stocks.Shared/ErrorCodes.cs` | 1 |
| `dotnet/Stocks.Persistence/Services/IDbmService.cs` | 1, 3 |
| `dotnet/Stocks.Persistence/Services/DbmService.cs` | 1, 3 |
| `dotnet/Stocks.EDGARScraper.Tests/Scoring/DbmInMemoryService.cs` (or equivalent) | 1, 3 |
| WebApi startup/routing file | 1 |
| `frontend/stocks-frontend/src/app/core/services/api.service.ts` | 2 |
| `frontend/stocks-frontend/src/app/features/scoring/scoring.component.ts` | 2 |
| `frontend/stocks-frontend/src/app/features/scoring/scoring.component.html` | 2 |
| `frontend/stocks-frontend/src/app/features/moat-scoring/moat-scoring.component.ts` | 2 |
| `frontend/stocks-frontend/src/app/features/moat-scoring/moat-scoring.component.html` | 2 |
| `dotnet/Stocks.DataModels/Scoring/CompanyScoreSummary.cs` | 3 |
| `dotnet/Stocks.DataModels/Scoring/CompanyMoatScoreSummary.cs` | 3 |
| `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyScoresStmt.cs` | 3 |
| `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyMoatScoresStmt.cs` | 3 |
| `dotnet/Stocks.Persistence/Database/Statements/GetCompanyScoresStmt.cs` | 3 |
| `dotnet/Stocks.Persistence/Database/Statements/GetCompanyMoatScoresStmt.cs` | 3 |
| `dotnet/Stocks.WebApi/Endpoints/ReportEndpoints.cs` | 3 |
| `dotnet/Stocks.WebApi/Endpoints/MoatReportEndpoints.cs` | 3 |
| `dotnet/Stocks.EDGARScraper/Program.cs` | 3 |
| `frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.ts` | 4 |
| `frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.html` | 4 |
| `frontend/stocks-frontend/src/app/features/moat-scores-report/moat-scores-report.component.ts` | 4 |
| `frontend/stocks-frontend/src/app/features/moat-scores-report/moat-scores-report.component.html` | 4 |

---

## Metadata

### Status
success

### Dependencies
- Price data must be imported for scored companies (via `--import-prices-stooq` or `--import-prices-stooq-bulk`)
- Scores must be pre-computed (via `--compute-all-scores` / `--compute-all-moat-scores`) for list pages
- Next available migration version number must be determined at implementation time

### Open Questions
- Should Stooq price data be treated as split-adjusted? (Affects return accuracy for companies that have split)
- What fixed lookback periods should the list page show? (Currently planned: 1 year only. Could add 3y, 5y later.)
- Should the detail page default start date be 1 year ago or `computedAt`? (Plan uses 1 year ago.)

### Assumptions
- Stooq daily close prices are sufficient for return calculations
- A single 1-year return column is sufficient for the initial list page implementation
- The investment return section on detail pages is simple enough to inline in both components without extracting a shared child component
- `DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1)` is an acceptable approximation for "1 year ago" (it does not account for leap years precisely, but this is fine for financial approximations)

<!-- Self-review: converged after 1 pass -->
