# Research: Moat Score — Alternate Scoring System with Frontend

## Table of Contents

- [1. Backend — Scoring Logic](#1-backend--scoring-logic)
  - [1.1. ScoringService.ComputeScore() Structure](#11-backend--scoring-logic---scoringservicecomputescore-structure)
  - [1.2. Derived Metrics Computation](#12-backend--scoring-logic---derived-metrics-computation)
  - [1.3. Scoring Checks Structure](#13-backend--scoring-logic---scoring-checks-structure)
  - [1.4. Concept Resolution Pattern](#14-backend--scoring-logic---concept-resolution-pattern)
  - [1.5. Shared vs New Concepts](#15-backend--scoring-logic---shared-vs-new-concepts)
- [2. Backend — New Metrics](#2-backend--new-metrics)
  - [2.1. New Metric Computations](#21-backend--new-metrics---new-metric-computations)
  - [2.2. Consistent Dividend or Buyback](#22-backend--new-metrics---consistent-dividend-or-buyback)
- [3. Backend — Data Layer](#3-backend--data-layer)
  - [3.1. GetScoringDataPointsStmt Query Design](#31-backend--data-layer---getscoringdatapointsstmt-query-design)
  - [3.2. company_scores Table Schema](#32-backend--data-layer---company_scores-table-schema)
  - [3.3. Bulk Score Computation End-to-End](#33-backend--data-layer---bulk-score-computation-end-to-end)
- [4. Backend — API](#4-backend--api)
  - [4.1. Scoring Endpoint Structure](#41-backend--api---scoring-endpoint-structure)
- [5. Frontend — List Page](#5-frontend--list-page)
  - [5.1. ScoresReportComponent](#51-frontend--list-page---scoresreportcomponent)
- [6. Frontend — Detail Page](#6-frontend--detail-page)
  - [6.1. ScoringComponent Structure](#61-frontend--detail-page---scoringcomponent-structure)
  - [6.2. Sparkline Implementation](#62-frontend--detail-page---sparkline-implementation)
- [7. Frontend — Navigation & Routing](#7-frontend--navigation--routing)
  - [7.1. Routes and Sidebar Pattern](#71-frontend--navigation--routing---routes-and-sidebar-pattern)
- [8. Frontend — API Service](#8-frontend--api-service)
  - [8.1. TypeScript Interfaces](#81-frontend--api-service---typescript-interfaces)
- [9. Testing](#9-testing)
  - [9.1. Existing Test Patterns](#91-testing---existing-test-patterns)
- [10. Cross-Cutting](#10-cross-cutting)
  - [10.1. Shared Abstractions](#101-cross-cutting---shared-abstractions)
- [11. Summary — Patterns to Follow](#11-summary--patterns-to-follow)
- [12. Summary — New Concepts and Metrics](#12-summary--new-concepts-and-metrics)
- [13. Risks and Concerns](#13-risks-and-concerns)
- [14. Recommended Approach](#14-recommended-approach)
- [15. Metadata](#15-metadata)

---

## 1. Backend — Scoring Logic

### 1.1. Backend — Scoring Logic - ScoringService.ComputeScore() Structure

**File:** `dotnet/Stocks.Persistence/Services/ScoringService.cs:372-469`

The `ComputeScore(ulong companyId)` method follows this flow:

1. **Fetch raw data:** Calls `_dbmService.GetScoringDataPoints(companyId, AllConceptNames, ct)` — fetches all XBRL data points for the given company filtered to the concept names in the static `AllConceptNames` array. Returns `IReadOnlyCollection<ScoringConceptValue>`.

2. **Fetch company info:** Gets `Company` by ID (for CIK lookup).

3. **Fetch tickers → price:** Gets tickers by company ID, takes the first ticker, then fetches prices by ticker to find the latest close price + date.

4. **Group and partition data:** Calls `GroupAndPartitionData(values)` which:
   - Builds `annualByYear`: Dictionary<int, Dictionary<string, decimal>> — only 10-K filings, grouped by year, first value per concept per year wins.
   - Builds `mostRecentSnapshot`: all concepts at the most recent report date (any filing type — used for balance sheet instant concepts only).
   - Extracts `oldestRetainedEarnings` from the oldest report date.
   - Builds `balanceTypes`: maps concept name → TaxonomyBalanceTypes (credit/debit).

5. **Compute derived metrics:** Calls `ComputeDerivedMetrics(rawDataByYear, mostRecentSnapshot, oldestRetainedEarnings, pricePerShare, sharesOutstanding, balanceTypes)` — produces a `DerivedMetrics` record with all computed values.

6. **Evaluate checks:** Calls `EvaluateChecks(metrics, yearsOfData)` — returns `IReadOnlyList<ScoringCheck>` (15 checks).

7. **Count scores:** Iterates checks, counts pass/computable.

8. **Assemble result:** Returns `ScoringResult` record.

**What can be reused for Moat Score:**
- `GroupAndPartitionData` — fully reusable (just needs a different/expanded concept name array).
- `ResolveField`, `ResolveEquity`, all resolution helpers — fully reusable.
- `ComputeDerivedMetrics` — partially reusable. The existing metrics (ROE CF, ROE OE, owner earnings, CapEx, dividends, debt-to-equity, estimated returns) are all needed by the Moat Score. However, new metrics (gross margin, operating margin, revenue CAGR, interest coverage) require new computation logic.
- `EvaluateChecks` — must be written fresh. The Moat Score has 13 different checks with different thresholds.
- The overall flow (fetch → group → compute metrics → evaluate checks → assemble) can follow the same pattern.

### 1.2. Backend — Scoring Logic - Derived Metrics Computation

**File:** `dotnet/Stocks.Persistence/Services/ScoringService.cs:770-951`

`ComputeDerivedMetrics` is a large static method that computes everything from the annual data. Key computations shared with Moat Score:

| Metric | How it's computed | Moat Score needs it? |
|--------|-------------------|---------------------|
| Equity | `ResolveEquity()` — derived from L&SE - Liab - NCI, or direct | Yes (for ROE, debt-to-equity) |
| Book Value | equity - goodwill - intangibles | No (Moat Score doesn't check book value) |
| Debt-to-Equity | debt / equity | Yes (check 9: < 1.0) |
| Net Cash Flow (per year) | grossCashFlow - netDebtIssuance - netStockIssuance - netPreferredIssuance | Yes (for ROE CF computation) |
| Owner Earnings (per year) | netIncome + depletionAndAmortization + deferredTax + otherNonCash - capEx + workingCapitalChange | Yes (for ROE OE, capex ratio, positive OE check) |
| ROE CF (per year) | 100 * netCashFlow / equity | Yes (averaged, check 1: >= 15%) |
| ROE OE (per year) | 100 * ownerEarnings / equity | Yes (averaged, check 2: >= 15%) |
| Average ROE CF | totalRoeCF / yearsWithRoeCF | Yes |
| Average ROE OE | totalRoeOE / yearsWithRoeOE | Yes |
| Average NCF | totalNetCashFlow / yearsWithNCF | Indirectly (for ROE CF; Moat estimated return uses OE only) |
| Average OE | totalOwnerEarnings / yearsWithOE | Yes (for estimated returns, capex ratio) |
| Estimated Return CF | 100 * (avgNCF - dividends) / marketCap | No (Moat Score uses OE-based return only) |
| Estimated Return OE | 100 * (avgOE - dividends) / marketCap | Yes (checks 12-13: > 3% and < 40%) |
| Dividends (per year) | ResolveField with DividendsChain | Yes (for consistent return check) |
| CapEx (per year) | ResolveField with CapExChain | Yes (for capex ratio) |

**Key observation:** The `ComputeDerivedMetrics` method already computes per-year owner earnings and per-year CapEx inside the loop, but only accumulates totals — it doesn't expose per-year values. The Moat Score needs per-year OE to check "positive OE every year" (check 6) and per-year CapEx for the capex ratio. The existing code accumulates `totalOwnerEarnings` and counts `yearsWithOE`, so the capex ratio can be derived from `averageCapEx / averageOwnerEarnings`. For the "positive OE every year" check, the Moat scoring needs to track per-year OE values or a count of failing years.

### 1.3. Backend — Scoring Logic - Scoring Checks Structure

**File:** `dotnet/Stocks.DataModels/Scoring/ScoringCheck.cs`

```csharp
public enum ScoringCheckResult { Pass, Fail, NotAvailable }
public record ScoringCheck(int CheckNumber, string Name, decimal? ComputedValue, string Threshold, ScoringCheckResult Result);
```

**File:** `dotnet/Stocks.Persistence/Services/ScoringService.cs:953-1053`

`EvaluateChecks` is a static method that creates a `List<ScoringCheck>(15)` and manually builds each check using `MakeCheck()`. It's hardcoded if/else per check — there's no generic check evaluator. Each check is:
- A check number (1-15)
- A name string
- The computed value
- A threshold string (for display)
- A result computed inline with ternary operators

**Can Moat Score reuse this?** The `ScoringCheck` and `ScoringCheckResult` types are fully reusable. The `MakeCheck` helper is reusable. The `EvaluateChecks` method itself must be written fresh for Moat (different checks, different thresholds), but the same pattern (list of ScoringCheck records) works perfectly.

### 1.4. Backend — Scoring Logic - Concept Resolution Pattern

**File:** `dotnet/Stocks.Persistence/Services/ScoringService.cs:25-194`

Concept resolution works via **static string arrays** (fallback chains) and a `ResolveField` method:

```csharp
internal static decimal? ResolveField(
    IReadOnlyDictionary<string, decimal> yearData,
    string[] fallbackChain,
    decimal? defaultValue)
```

The `AllConceptNames` array (lines 25-143) lists all ~144 XBRL concept names fetched from the database. Fallback chains are defined as separate static arrays (e.g., `EquityChain`, `DebtChain`, `NetIncomeChain`). Resolution iterates the chain and returns the first match.

**Adding new concepts:** To add Revenue, COGS, GrossProfit, OperatingIncomeLoss, InterestExpense:
1. Add the concept names to a new `MoatConceptNames` array (or extend `AllConceptNames`).
2. Define new fallback chains (e.g., `RevenueChain`, `CostOfGoodsChain`, `GrossProfitChain`, `OperatingIncomeChain`, `InterestExpenseChain`).
3. Use `ResolveField` with the new chains in the Moat metric computation.

Note: Revenue concepts already exist in `CompanyEndpoints.cs:91-102` (`ArRevenueConceptNames`) with a `RevenueFallbackChain` (lines 111-116). This chain can be reused or referenced.

### 1.5. Backend — Scoring Logic - Shared vs New Concepts

Cross-referencing the Moat Score's 13 checks with existing `AllConceptNames`:

**Already in `AllConceptNames` (and computed by Value Score):**
- Equity concepts (for ROE): all present
- Net Income concepts (for OE): `NetIncomeLoss`, `IncomeLossFromContinuingOperations`, `ProfitLoss`
- Cash flow concepts (for NCF): all present
- Debt concepts: `LongTermDebt*` chains
- CapEx: `PaymentsToAcquirePropertyPlantAndEquipment`
- Dividends: `PaymentsOfDividends*` chain
- Stock repurchase: `PaymentsForRepurchaseOfCommonStock`, `PaymentsForRepurchaseOfEquity`
- Depreciation/amortization (for OE): all present
- Working capital (for OE): all present
- Shares outstanding: all present
- Market cap components (price + shares): available

**NOT in `AllConceptNames` — NEW concepts needed:**

| Concept | XBRL Tags | Used For |
|---------|-----------|----------|
| Revenue | `Revenues`, `RevenueFromContractWithCustomerExcludingAssessedTax`, `SalesRevenueNet` | Gross margin, operating margin, revenue CAGR |
| COGS | `CostOfGoodsAndServicesSold`, `CostOfRevenue`, `CostOfGoodsSold` | Gross margin |
| Gross Profit | `GrossProfit` | Gross margin (direct alternative) |
| Operating Income | `OperatingIncomeLoss` | Operating margin, interest coverage |
| Interest Expense | `InterestExpense`, `InterestExpenseDebt` | Interest coverage |

Note: Revenue concepts exist in `CompanyEndpoints.ArRevenueConceptNames` but are NOT in `ScoringService.AllConceptNames`. They will need to be added to the Moat concept set.

---

## 2. Backend — New Metrics

### 2.1. Backend — New Metrics - New Metric Computations

Based on the design doc (`docs/moat-score-design.md`) and existing patterns:

**Gross Margin (per year):**
- Formula: `GrossProfit / Revenue * 100`
- Fallback: If `GrossProfit` not available, compute `Revenue - COGS` as gross profit.
- Concepts needed: `GrossProfit` (direct), `Revenue` (fallback chain: `Revenues` → `RevenueFromContractWithCustomerExcludingAssessedTax` → `SalesRevenueNet`), `COGS` (fallback chain: `CostOfGoodsAndServicesSold` → `CostOfRevenue` → `CostOfGoodsSold`).
- Averaging: Compute per year, then average across years (matching Value Score pattern).
- If neither Gross Profit nor COGS is available for a year, exclude that year.
- Check returns `NotAvailable` if no years have computable gross margin.

**Operating Margin (per year):**
- Formula: `OperatingIncomeLoss / Revenue * 100`
- Concepts needed: `OperatingIncomeLoss` (single concept), `Revenue` (same chain as above).
- Same averaging pattern.

**Revenue CAGR:**
- Formula: `(Revenue_latest / Revenue_oldest) ^ (1 / years) - 1`
- Uses oldest and most recent fiscal years with revenue data from the annual set.
- Needs at least 2 years with revenue data.

**CapEx Ratio:**
- Formula: `Average annual CapEx / Average annual Owner Earnings * 100`
- Both CapEx and OE are already computed per year in `ComputeDerivedMetrics`. The existing code accumulates `totalOwnerEarnings` and `yearsWithOE`, and CapEx is resolved per year. However, average CapEx is not currently tracked — it would need to be accumulated similarly.

**Interest Coverage:**
- Formula: `OperatingIncome / InterestExpense` (most recent fiscal year)
- Concepts: `OperatingIncomeLoss` / (`InterestExpense` → `InterestExpenseDebt`).
- Single year computation, not an average.

**Positive OE Every Year:**
- Track count of years where OE <= 0.
- Check passes if 0 failing years.
- Existing OE computation loop already calculates per-year OE; just need to count non-positive years.

### 2.2. Backend — New Metrics - Consistent Dividend or Buyback

**Design doc check 8:** "Returned capital in >= 75% of years."

Existing Value Score already resolves per-year:
- Dividends: `DividendsChain` = `PaymentsOfDividends` → `PaymentsOfDividendsCommonStock` → `Dividends` → `DividendsCommonStockCash`
- Stock repurchase: `StockRepurchaseChain` = `PaymentsForRepurchaseOfCommonStock` → `PaymentsForRepurchaseOfEquity`

Both are resolved in the `ComputeDerivedMetrics` per-year loop (lines 888-893 in ScoringService.cs).

**Implementation:** For each year, compute `returnedCapital = dividends + stockRepurchase`. If `returnedCapital > 0`, that year counts as "returning capital." Track `yearsWithReturn` / `totalYears`. Check passes if `yearsWithReturn >= 0.75 * totalYears`.

Note on sign convention: Dividends paid and stock repurchase are cash outflows. In the existing code, `dividends = ResolveField(yearData, DividendsChain, 0m)` and `stockRepurch = ResolveField(yearData, StockRepurchaseChain, 0m)`. These values are typically positive (they represent payments). So `dividends + stockRepurchase > 0` means capital was returned.

---

## 3. Backend — Data Layer

### 3.1. Backend — Data Layer - GetScoringDataPointsStmt Query Design

**File:** `dotnet/Stocks.Persistence/Database/Statements/GetScoringDataPointsStmt.cs`

The SQL query accepts `@company_id` and `@concept_names` (PostgreSQL text array) as parameters. It:
1. Finds the 5 most recent annual (filing_type=1, 10-K) report dates that have data for any of the given concepts.
2. Finds the single most recent report date across all filing types (10-K or 10-Q).
3. Unions these dates and fetches all matching data points.
4. Uses `DISTINCT ON` to handle duplicate data points per concept per submission.

**What changes for Moat Score?** The query is fully parameterized on concept names. Passing a different/expanded concept name array is sufficient — no SQL changes needed. The Moat Score would either:
- Pass a combined array (Value + Moat concepts) — increases query scope but works.
- Pass a Moat-specific array — cleaner separation.
- Note: The query limits to 5 annual dates. The Moat Score requires 7 years of history. This `LIMIT 5` must be increased to `LIMIT 8` (or made parameterizable) for the Moat Score queries.

**`GetAllScoringDataPointsStmt`** (`dotnet/Stocks.Persistence/Database/Statements/GetAllScoringDataPointsStmt.cs`) works identically but for all companies at once. Same issue: `rn <= 5` must be increased for Moat Score.

### 3.2. Backend — Data Layer - company_scores Table Schema

**File:** `dotnet/Stocks.Persistence/Database/Migrations/V013__AddCompanyScoresTable.sql` (plus V014, V015, V016 addenda)

Current schema:
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
    computed_at timestamptz NOT NULL DEFAULT NOW(),
    -- V014:
    current_dividends_paid decimal,
    max_buy_price decimal,
    percentage_upside decimal,
    -- V016:
    average_roe_cf decimal,
    average_roe_oe decimal
);
```

**Recommendation:** Create a separate `company_moat_scores` table. Reasons:
1. Different column set (Moat needs gross margin, operating margin, revenue CAGR, capex ratio, interest coverage — none of which exist in the Value Score table).
2. Clean separation — both scoring systems operate independently.
3. Moat summary columns: `company_id`, `cik`, `company_name`, `ticker`, `exchange`, `overall_score`, `computable_checks`, `years_of_data`, `average_gross_margin`, `average_operating_margin`, `average_roe_cf`, `average_roe_oe`, `estimated_return_oe`, `revenue_cagr`, `capex_ratio`, `interest_coverage`, `debt_to_equity_ratio`, `price_per_share`, `price_date`, `shares_outstanding`, `computed_at`.

### 3.3. Backend — Data Layer - Bulk Score Computation End-to-End

**CLI command:** `--compute-all-scores` in `Program.cs:181-188`

**Flow:**
1. `ComputeAndStoreAllScoresAsync()` (lines 971-995):
   - Creates `ScoringService` with `_dbm` and `_logger`.
   - Calls `scoringService.ComputeAllScores(ct)` — returns `Result<IReadOnlyCollection<CompanyScoreSummary>>`.
   - Truncates `company_scores` table via `_dbm.TruncateCompanyScores(ct)`.
   - Bulk inserts all scores via `_dbm.BulkInsertCompanyScores(scores, ct)`.

2. `ComputeAllScores` (lines 198-370 in ScoringService.cs):
   - Single query to fetch all scoring data points (`GetAllScoringDataPointsStmt`).
   - Single query for all latest prices.
   - Single query for all tickers.
   - Single query for all company names.
   - Single query for all companies.
   - Builds lookup dictionaries.
   - Groups data by company.
   - Iterates each company: `GroupAndPartitionData` → `ComputeDerivedMetrics` → `EvaluateChecks` → build `CompanyScoreSummary`.

**For Moat Score:** A `--compute-all-moat-scores` command would follow the same pattern:
1. Create `MoatScoringService` (new class).
2. Call `moatService.ComputeAllMoatScores(ct)`.
3. Truncate `company_moat_scores`.
4. Bulk insert via `BulkInsertCompanyMoatScores`.

The `ComputeAllScores` batch pattern can be copied and adapted. The main changes are:
- Different concept names array (expanded with Revenue, COGS, etc.).
- Different metrics computation (new `MoatDerivedMetrics`).
- Different check evaluation (13 Moat checks).
- Different summary record (`CompanyMoatScoreSummary`).

---

## 4. Backend — API

### 4.1. Backend — API - Scoring Endpoint Structure

**File:** `dotnet/Stocks.WebApi/Endpoints/ScoringEndpoints.cs`

Single endpoint: `GET /api/companies/{cik}/scoring`
- Resolves company by CIK.
- Calls `scoringService.ComputeScore(company.CompanyId, ct)` for on-demand computation.
- Returns JSON with: `rawDataByYear`, `metrics` (all DerivedMetrics fields), `scorecard` (array of check objects), `overallScore`, `computableChecks`, `yearsOfData`, `pricePerShare`, `priceDate`, `sharesOutstanding`, `maxBuyPrice`, `percentageUpside`.

**File:** `dotnet/Stocks.WebApi/Endpoints/ReportEndpoints.cs`

Single endpoint: `GET /api/reports/scores`
- Reads from pre-computed `company_scores` table.
- Supports pagination, sorting (10 sort columns), filtering (minScore, maxScore, exchange).
- Returns `PagedResults<CompanyScoreSummary>`.

**For Moat Score, need three new endpoints:**

1. **`GET /api/companies/{cik}/moat-scoring`** — On-demand single-company Moat Score computation. Same pattern as `ScoringEndpoints.cs`. Returns `MoatScoringResult` with moat-specific metrics, scorecard, and trend data.

2. **`GET /api/reports/moat-scores`** — Pre-computed Moat Scores list/report. Same pattern as `ReportEndpoints.cs` but reads from `company_moat_scores`. Different sort columns (gross margin, operating margin, revenue CAGR, etc.).

3. **Trend data** can be included in the on-demand endpoint response (just like AR/Revenue is currently fetched separately). The per-year metrics (gross margin %, operating margin %, ROE CF %, ROE OE %, revenue) can be returned as arrays alongside the scoring result. Alternatively, a dedicated `GET /api/companies/{cik}/moat-trends` endpoint.

**Cleanest approach:** Create `MoatScoringEndpoints.cs` and `MoatReportEndpoints.cs` as new files. This keeps Moat endpoints fully independent from Value Score endpoints.

---

## 5. Frontend — List Page

### 5.1. Frontend — List Page - ScoresReportComponent

**File:** `frontend/stocks-frontend/src/app/features/scores-report/scores-report.component.ts`

**Structure:**
- Standalone component with signals for reactive state.
- **Data fetching:** `fetchScores()` calls `api.getScoresReport(params)` which hits `GET /api/reports/scores`. Parameters: page, pageSize, sortBy, sortDir, minScore, exchange.
- **Table rendering:** Uses `@for` loop over `items()` signal. Each row has: Score badge, Company (link to detail), Ticker, Exchange, Price, Max Buy Price, % Upside, Market Cap, Est. Return CF/OE, Avg ROE CF/OE.
- **Column definitions:** Hardcoded in template. Sortable columns use `(click)="toggleSort('columnName')"`.
- **Sorting:** `toggleSort()` flips direction if same column, else sets new column with desc default.
- **Filtering:** Min Score dropdown (Any, 10+, 9+, 8+, 7+, 5+), Exchange dropdown (All, NASDAQ, NYSE, CBOE), Page Size dropdown (25, 50, 100).
- **Pagination:** Previous/Next buttons with page/totalPages display.
- **Color coding:** `scoreBadgeClass(score)` — green (>=10), yellow (>=7), red (<7). `rowHighlightClass` — green row for perfect (15/15), yellow for near-perfect (14/15).
- **Formatting helpers:** `fmtPrice`, `fmtCurrency`, `fmtRatio`, `fmtPct`.

**Moat Scores list page columns (per design doc):** Score, Company, Ticker, Exchange, Price, Gross Margin, Operating Margin, Avg ROE (CF/OE), Est. Return (OE), Revenue CAGR. Different from Value Score columns but same structural pattern.

---

## 6. Frontend — Detail Page

### 6.1. Frontend — Detail Page - ScoringComponent Structure

**File:** `frontend/stocks-frontend/src/app/features/scoring/scoring.component.ts`

**Sections rendered (in order):**
1. **Breadcrumb:** Home / CIK / Value Score
2. **Company header:** Name, CIK, price, tickers with badges, external links (Yahoo Finance, Google Finance)
3. **Score badge:** Large colored badge (green/yellow/red) showing score/computableChecks
4. **Score caption:** Years of data, price, shares outstanding
5. **Scorecard table:** Check #, Check name, Value, Threshold, Result (pass/fail/na indicators)
6. **Derived Metrics table:** 16 rows of label/value pairs
7. **AR/Revenue Trend:** Table + sparkline side by side (only if data available)
8. **Raw Data table:** All concepts by year

**Data flow:**
- `ngOnInit()` makes three parallel API calls: `getCompany(cik)`, `getScoring(cik)`, `getArRevenue(cik)`.
- All state stored in signals: `company`, `scoring`, `loading`, `error`, `arRevenueRows`.
- Computed signals: `sparklineData`, `metricRows`, `yearKeys`, `rawRows`.

**Moat detail page:** Same structure but:
- Breadcrumb: Home / CIK / Moat Score
- Moat-specific scorecard (13 checks)
- Moat-specific derived metrics
- 6 trend charts instead of 1 (AR/Revenue, Gross Margin, Operating Margin, ROE CF, ROE OE, Revenue)

### 6.2. Frontend — Detail Page - Sparkline Implementation

**File:** `frontend/stocks-frontend/src/app/features/scoring/scoring.component.ts:372-438`

The sparkline is a computed signal `sparklineData` that produces SVG data:

1. **Data prep:** Takes `arRevenueRows`, reverses to chronological, filters to rows with non-null ratio. Requires >= 2 points.

2. **Layout constants:**
   - SVG viewBox: `0 0 240 120`
   - Padding: left=35, right=10, top=10, bottom=20
   - Plot area: 195 x 90 pixels

3. **Y-axis scaling:**
   - Minimum is always 0.
   - Maximum: rounds up to nearest nice number (ceil to whole %, then round to nearest 5% if > 5%).
   - Tick step: 1% if max <= 10%, 5% if max <= 30%, 10% otherwise.
   - Generates `yTicks` array with `{ y, label }`.

4. **Point mapping:**
   - X: evenly distributed across plot width (`padLeft + (i / (n-1)) * plotW`).
   - Y: `padTop + plotH - ((value - min) / range) * plotH`.
   - Produces `points` array with `{ x, y, year, ratio }`.

5. **SVG elements rendered:**
   - Grid lines (horizontal) at each Y tick.
   - Y-axis labels (percentage).
   - Vertical and horizontal axis lines.
   - Polyline connecting all points (blue, stroke-width 2).
   - Circle at each point (r=3, blue fill) with tooltip.
   - X-axis labels (year) below each point.

**Reuse for Moat Score:** This pattern can be extracted into a reusable helper or computed signal factory. Each of the 6 Moat trend charts needs the same SVG structure with different data. The inputs would be: array of `{ year, value }`, y-axis label format (percentage vs currency), and optional styling.

---

## 7. Frontend — Navigation & Routing

### 7.1. Frontend — Navigation & Routing - Routes and Sidebar Pattern

**File:** `frontend/stocks-frontend/src/app/app.routes.ts`

Routes use lazy-loaded standalone components:
```typescript
{ path: 'scores', title: 'Stocks - Scores', loadComponent: () => import('...').then(m => m.ScoresReportComponent) },
{ path: 'company/:cik/scoring', loadComponent: () => import('...').then(m => m.ScoringComponent) },
```

**To add Moat Score routes:**
```typescript
{ path: 'moat-scores', title: 'Stocks - Moat Scores', loadComponent: () => import('...').then(m => m.MoatScoresReportComponent) },
{ path: 'company/:cik/moat-scoring', loadComponent: () => import('...').then(m => m.MoatScoringComponent) },
```

**File:** `frontend/stocks-frontend/src/app/core/layout/sidebar/sidebar.component.ts`

Sidebar is a simple list of `<li>` with `routerLink` and `routerLinkActive`. Each entry is:
```html
<a routerLink="/scores" routerLinkActive="active" title="...">Value Scores</a>
```

**To add Moat Scores entry:** Add another `<li>` below "Value Scores":
```html
<a routerLink="/moat-scores" routerLinkActive="active" title="Rewards competitive moat: high margins, consistent returns, capital-light operations, and long track records">Moat Scores</a>
```

---

## 8. Frontend — API Service

### 8.1. Frontend — API Service - TypeScript Interfaces

**File:** `frontend/stocks-frontend/src/app/core/services/api.service.ts`

**Reusable interfaces:**
- `ScoringCheckResponse` — fully reusable (same check structure: checkNumber, name, computedValue, threshold, result).
- `PaginationResponse` / `PaginatedResponse<T>` — fully reusable for the Moat list page.
- `ScoresReportParams` — reusable structure (page, pageSize, sortBy, sortDir, minScore, exchange).
- `CompanyDetail` — reusable (used by company header).

**Need Moat-specific equivalents:**
- `MoatDerivedMetricsResponse` — different fields: averageGrossMargin, averageOperatingMargin, averageRoeCF, averageRoeOE, estimatedReturnOE, revenueCagr, capexRatio, interestCoverage, debtToEquityRatio, currentDividendsPaid, marketCap, etc.
- `MoatScoringResponse` — wraps moat metrics, scorecard, per-year trend data, overall score, etc.
- `CompanyMoatScoreSummary` — summary record for the list page (different columns from `CompanyScoreSummary`).
- `MoatTrendRow` — for per-year trend data: `{ year, grossMargin, operatingMargin, roeCF, roeOE, revenue, arRevenueRatio }`.

**New API methods needed:**
- `getMoatScoring(cik: string): Observable<MoatScoringResponse>`
- `getMoatScoresReport(params: ScoresReportParams): Observable<PaginatedResponse<CompanyMoatScoreSummary>>`

---

## 9. Testing

### 9.1. Testing - Existing Test Patterns

**Test files in `dotnet/Stocks.EDGARScraper.Tests/Scoring/`:**

1. **`ScoringServiceTests.cs`** — Unit tests for static helper methods: `ResolveField`, `ResolveEquity`, `ResolveDepletionAndAmortization`, `ResolveDeferredTax`, `ResolveOtherNonCash`, `ResolveWorkingCapitalChange`, `ComputeDerivedMetrics`, `EvaluateChecks`, `GroupAndPartitionData`, `ComputeMaxBuyPrice`, `ComputePercentageUpside`. Uses direct method calls on static methods with `Dictionary<string, decimal>` inputs. Over 600 lines of tests.

2. **`ScoringModelTests.cs`** — Tests for model records: `ScoringResult` score counting, `DerivedMetrics` all-null construction.

3. **`BatchScoringServiceTests.cs`** — Integration-style tests using `DbmInMemoryService`. Seeds taxonomy concepts, companies, submissions, data points, and prices via helper methods. Tests `ComputeAllScores` end-to-end.

4. **`GetScoringDataPointsTests.cs`** — Tests for the data-fetching query logic using in-memory data.

5. **`ArRevenueTests.cs`** — Tests for AR/Revenue resolution logic.

**Patterns used:**
- xUnit with `[Fact]` attributes.
- Descriptive method names (e.g., `ResolveEquity_PrefersLiabilitiesAndEquityMinusLiabilities`).
- Direct construction of `Dictionary<string, decimal>` for unit tests.
- `DbmInMemoryService` + `DbmInMemoryData` for integration tests (no real database).
- Seeding helpers for creating test companies, submissions, data points.
- `Assert.Equal`, `Assert.Null`, `Assert.NotNull`, `Assert.True` assertions.

**Moat Score tests should cover:**
- New metric computations: gross margin, operating margin, revenue CAGR, capex ratio, interest coverage.
- New check evaluations: all 13 Moat checks with pass/fail/na scenarios.
- Concept resolution for new chains (Revenue, COGS, GrossProfit, OperatingIncomeLoss, InterestExpense).
- "Positive OE every year" edge cases (one failing year, all passing, no data).
- "Consistent dividend/buyback" threshold (75% of years).
- Batch computation (`ComputeAllMoatScores`) end-to-end.

---

## 10. Cross-Cutting

### 10.1. Cross-Cutting - Shared Abstractions

**There is no `IScoringService` interface.** `ScoringService` is a concrete class with no base class or interface. It is constructed directly in:
- `ScoringEndpoints.cs` — registered as a service (likely via DI in WebApi startup).
- `Program.cs` — constructed directly with `new ScoringService(_dbm, _logger)`.

**There are no generic scoring base classes.** The code is monolithic: one `ScoringService` class handles all Value Score logic.

**Recommendation:** Create a separate `MoatScoringService` class. Reasons:
1. The scoring logic is fundamentally different (different checks, different thresholds, new metrics).
2. Extracting shared code into a base class would add complexity without much benefit — the shared parts are all static helper methods that can simply be called from the new service.
3. The static helpers (`ResolveField`, `ResolveEquity`, `GroupAndPartitionData`, etc.) in `ScoringService` are already `internal static` and can be called from `MoatScoringService` within the same assembly.

**Shared code path:**
- `ScoringService.ResolveField` — call directly.
- `ScoringService.ResolveEquity` — call directly.
- `ScoringService.GroupAndPartitionData` — call directly.
- `ScoringService.ComputeDerivedMetrics` — consider extracting a subset, or have `MoatScoringService` call the existing method and add Moat-specific metrics on top.
- `ScoringCheck`, `ScoringCheckResult`, `ScoringConceptValue`, `BatchScoringConceptValue` — reuse directly.
- `MakeCheck` — currently private, would need to be made internal, or duplicated (it's a one-liner).

---

## 11. Summary — Patterns to Follow

1. **Concept resolution:** Define static fallback chain arrays + use `ResolveField` for each concept.
2. **Data fetching:** Parameterized SQL with concept name arrays via `GetScoringDataPointsStmt` / `GetAllScoringDataPointsStmt` (with increased year limit).
3. **Per-year computation → averaging:** Loop over sorted years, compute metric per year, accumulate totals, divide by count at the end.
4. **Check evaluation:** Build a `List<ScoringCheck>`, each check is a record with pass/fail/na.
5. **Batch computation:** Fetch all data in one query, build lookups, iterate companies, write `CompanyMoatScoreSummary` records.
6. **API endpoints:** Minimal API with `MapGet`, return anonymous objects or records.
7. **Frontend components:** Standalone Angular components, signals for state, computed signals for derived data, inline templates/styles.
8. **Sparkline SVG:** Computed signal producing polyline + circles from data array.
9. **Routing:** Lazy-loaded components via `loadComponent`.
10. **Testing:** Static method unit tests + `DbmInMemoryService` integration tests.

---

## 12. Summary — New Concepts and Metrics

**New XBRL concepts to import (5 concept groups, 10 total tags):**

| Group | Tags | Fallback Chain |
|-------|------|----------------|
| Revenue | `Revenues`, `RevenueFromContractWithCustomerExcludingAssessedTax`, `SalesRevenueNet` | First match |
| COGS | `CostOfGoodsAndServicesSold`, `CostOfRevenue`, `CostOfGoodsSold` | First match |
| Gross Profit | `GrossProfit` | Direct (alternative to Revenue - COGS) |
| Operating Income | `OperatingIncomeLoss` | Single concept |
| Interest Expense | `InterestExpense`, `InterestExpenseDebt` | First match |

**New metrics to compute:**

| Metric | Formula | Averaging |
|--------|---------|-----------|
| Gross Margin | GrossProfit / Revenue * 100 | Per year then avg |
| Operating Margin | OperatingIncome / Revenue * 100 | Per year then avg |
| Revenue CAGR | (latest/oldest)^(1/years) - 1 | Single computation |
| CapEx Ratio | Avg CapEx / Avg OE * 100 | From averages |
| Interest Coverage | OperatingIncome / InterestExpense | Most recent year |
| Positive OE Years | Count of years with OE > 0 | Per year count |
| Capital Return Years | Count of years with dividends + repurchases > 0 | Per year count |

---

## 13. Risks and Concerns

1. **XBRL concept availability:** Not all companies report `GrossProfit`, `OperatingIncomeLoss`, or `InterestExpense`. The design doc addresses this with fallback chains (e.g., GrossProfit = Revenue - COGS) and `NotAvailable` results. However, financial companies (banks, insurance) typically don't report gross profit or operating income in the standard GAAP taxonomy, so many financial companies will have `NotAvailable` for margin checks.

2. **Year limit in data queries:** The current `GetScoringDataPointsStmt` and `GetAllScoringDataPointsStmt` limit to 5 annual dates. The Moat Score requires 7+ years of history (check 11). This limit must be increased. Two approaches: (a) make it a parameter, (b) create Moat-specific statement classes with a higher limit (e.g., 8 or 10 years). Approach (a) is cleaner.

3. **Performance of additional queries:** Adding 10 new concept names to `AllConceptNames` will increase the data fetched per query. For the batch query (`GetAllScoringDataPointsStmt`), which already has a 600-second timeout, this may increase query time. Mitigated by the fact that these are well-indexed joins.

4. **Frontend complexity:** 6 trend charts on one page (each with table + sparkline) is a significant amount of rendering. Consider:
   - Extracting a shared sparkline component to avoid duplicating 60+ lines of SVG template 6 times.
   - Lazy-rendering charts (only render visible section).

5. **Separate vs combined concept arrays:** The Moat Score could either extend `AllConceptNames` (simplest but couples the two systems) or maintain its own `MoatConceptNames` array (cleaner separation but duplicates shared concepts). Recommendation: `MoatConceptNames` should be a new array combining the shared concepts (equity, debt, cash flow, etc.) with Moat-specific ones (Revenue, COGS, etc.).

6. **Revenue concept already in CompanyEndpoints:** The `ArRevenueConceptNames` and `RevenueFallbackChain` in `CompanyEndpoints.cs` already define Revenue resolution. The Moat Score should use the same fallback chain for consistency.

---

## 14. Recommended Approach

**Principle:** Keep Moat Score fully independent from Value Score while maximizing code reuse via shared static helpers and shared data models.

**Backend:**
1. Create `MoatScoringService.cs` in `Stocks.Persistence/Services/` — new class, not extending `ScoringService`.
2. Define `MoatConceptNames` array combining shared + new concepts.
3. Define new fallback chains: `RevenueChain`, `CostOfGoodsChain`, `GrossProfitChain`, `OperatingIncomeChain`, `InterestExpenseChain`.
4. Create `MoatDerivedMetrics` record in `Stocks.DataModels/Scoring/`.
5. Implement `ComputeMoatDerivedMetrics` — calls existing `ResolveField`, `ResolveEquity`, etc., adds new metric computations.
6. Implement `EvaluateMoatChecks` — 13 checks per the design doc.
7. Create `MoatScoringResult` and `CompanyMoatScoreSummary` records.
8. Create `company_moat_scores` migration.
9. Create `BulkInsertCompanyMoatScoresStmt` and `GetCompanyMoatScoresStmt`.
10. Parameterize the year limit in `GetScoringDataPointsStmt` / `GetAllScoringDataPointsStmt` (or create Moat-specific versions).
11. Create `MoatScoringEndpoints.cs` and `MoatReportEndpoints.cs`.
12. Add `--compute-all-moat-scores` CLI command in `Program.cs`.

**Frontend:**
1. Create `MoatScoringComponent` — new component modeled on `ScoringComponent`.
2. Create `MoatScoresReportComponent` — new component modeled on `ScoresReportComponent`.
3. Extract a reusable sparkline utility/helper for shared SVG generation.
4. Add Moat TypeScript interfaces in `api.service.ts`.
5. Add API methods: `getMoatScoring`, `getMoatScoresReport`.
6. Add routes in `app.routes.ts`.
7. Add sidebar entry.

---

## 15. Metadata

### Status
success

### Dependencies
- `docs/moat-score-design.md` — authoritative source for check definitions and thresholds
- `dotnet/Stocks.Persistence/Services/ScoringService.cs` — shared static helpers
- `dotnet/Stocks.DataModels/Scoring/` — shared model types

### Open Questions
- None

### Assumptions
- The Moat Score will be fully independent of the Value Score (separate service, separate table, separate endpoints).
- Revenue fallback chain will match the one already defined in `CompanyEndpoints.cs`.
- The year limit in data queries will be increased (from 5 to 8+) to support the 7-year history requirement.
- Financial companies (banks, insurance) will often receive `NotAvailable` for margin-based checks due to XBRL taxonomy differences — this is acceptable.

<!-- Self-review: converged after 2 passes -->
