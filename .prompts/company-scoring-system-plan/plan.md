# Plan: Company Scoring System (13-Point Value Score)

## Table of Contents

- [1. Overview](#1-overview)
- [2. Checkpoints](#2-checkpoints)
  - [2.1. Checkpoints - Checkpoint 1: Scoring Data Models](#21-checkpoints---checkpoint-1-scoring-data-models)
  - [2.2. Checkpoints - Checkpoint 2: Data Access Layer](#22-checkpoints---checkpoint-2-data-access-layer)
  - [2.3. Checkpoints - Checkpoint 3: Scoring Computation Service](#23-checkpoints---checkpoint-3-scoring-computation-service)
  - [2.4. Checkpoints - Checkpoint 4: API Endpoint](#24-checkpoints---checkpoint-4-api-endpoint)
  - [2.5. Checkpoints - Checkpoint 5: Frontend Scoring Page](#25-checkpoints---checkpoint-5-frontend-scoring-page)
- [Metadata](#metadata)

## 1. Overview

Add a per-company scoring page computing a 13-point value score based on US-GAAP financial data from 10-K filings. The system fetches up to 5 years of annual data, resolves US-GAAP concept fallback chains, computes derived metrics, and evaluates 13 value-investing checks.

**Architecture flow:**
```
DB (data_points + prices) → IDbmService → ScoringService → ScoringEndpoints → Angular ScoringComponent
```

**US-GAAP concepts to query** (33 distinct names across 16 fallback chains — see research.md section 6.1):

```
-- Balance sheet (instant)
StockholdersEquity
StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest
Goodwill
IntangibleAssetsNetExcludingGoodwill
LongTermDebtAndCapitalLeaseObligations
LongTermDebt
LongTermDebtNoncurrent
RetainedEarningsAccumulatedDeficit
AssetsCurrent
LiabilitiesCurrent
CommonStockSharesOutstanding

-- Cash flow (duration)
CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect
CashAndCashEquivalentsPeriodIncreaseDecrease
ProceedsFromIssuanceOfLongTermDebt
RepaymentsOfLongTermDebt
PaymentsOfDividends
PaymentsOfDividendsCommonStock
Dividends
ProceedsFromIssuanceOfCommonStock
PaymentsForRepurchaseOfCommonStock
ProceedsFromIssuanceOfPreferredStockAndPreferenceStock
PaymentsForRepurchaseOfPreferredStockAndPreferenceStock
IncreaseDecreaseInOperatingCapital
IncreaseDecreaseInOtherOperatingCapitalNet
DeferredIncomeTaxExpenseBenefit
DeferredIncomeTaxesAndTaxCredits
Depletion
AmortizationOfIntangibleAssets
OtherNoncashIncomeExpense
PaymentsToAcquirePropertyPlantAndEquipment

-- Income statement (duration)
NetIncomeLoss
IncomeLossFromContinuingOperations
ProfitLoss
```

## 2. Checkpoints

### 2.1. Checkpoints - Checkpoint 1: Scoring Data Models

Create the data models needed for scoring input and output.

#### Build

**File: `dotnet/Stocks.DataModels/Scoring/ScoringConceptValue.cs`**
```csharp
// A single raw data point value for scoring
public record ScoringConceptValue(string ConceptName, decimal Value, DateOnly ReportDate);
```

**File: `dotnet/Stocks.DataModels/Scoring/ScoringCheck.cs`**
```csharp
public enum ScoringCheckResult { Pass, Fail, NotAvailable }

public record ScoringCheck(
    int CheckNumber,
    string Name,
    decimal? ComputedValue,   // null if not computable
    string Threshold,          // e.g. "< 0.5", "> $150M"
    ScoringCheckResult Result);
```

**File: `dotnet/Stocks.DataModels/Scoring/DerivedMetrics.cs`**
```csharp
// All intermediate computed values. Nullable fields = not computable.
public record DerivedMetrics(
    decimal? BookValue,
    decimal? MarketCap,
    decimal? DebtToEquityRatio,
    decimal? PriceToBookRatio,
    decimal? DebtToBookRatio,
    decimal? AdjustedRetainedEarnings,
    decimal? OldestRetainedEarnings,
    decimal? AverageNetCashFlow,
    decimal? AverageOwnerEarnings,
    decimal? EstimatedReturnCF,
    decimal? EstimatedReturnOE,
    decimal? CurrentDividendsPaid);
```

**File: `dotnet/Stocks.DataModels/Scoring/ScoringResult.cs`**
```csharp
public record ScoringResult(
    // Raw data: concept name → value, grouped by report year
    IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> RawDataByYear,
    DerivedMetrics Metrics,
    IReadOnlyList<ScoringCheck> Scorecard,
    int OverallScore,          // checks that passed
    int ComputableChecks,      // checks that could be evaluated (not N/A)
    int YearsOfData,
    decimal? PricePerShare,
    DateOnly? PriceDate,
    long? SharesOutstanding);
```

#### Test

**File: `dotnet/Stocks.EDGARScraper.Tests/Scoring/ScoringModelTests.cs`**

- `ScoringResult_OverallScoreDoesNotExceedComputableChecks` — construct a `ScoringResult` with mixed pass/fail/N/A checks, verify OverallScore ≤ ComputableChecks ≤ 13
- `DerivedMetrics_AllFieldsNullable` — construct `DerivedMetrics` with all nulls, verify no exceptions (baseline for "no data" scenario)

#### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

### 2.2. Checkpoints - Checkpoint 2: Data Access Layer

Add a new DB statement and IDbmService method to fetch scoring concept values for a company across its 10-K filings.

#### Build

**File: `dotnet/Stocks.Persistence/Database/Statements/GetScoringDataPointsStmt.cs`**

New statement inheriting `QueryDbStmtBase`. SQL:

```sql
SELECT tc.name AS concept_name, dp.value, s.report_date
FROM data_points dp
JOIN taxonomy_concepts tc ON dp.taxonomy_concept_id = tc.taxonomy_concept_id
JOIN submissions s ON dp.submission_id = s.submission_id AND dp.company_id = s.company_id
WHERE dp.company_id = @company_id
  AND s.filing_type = 1
  AND dp.end_date = s.report_date
  AND tc.name = ANY(@concept_names)
  AND s.report_date IN (
    SELECT DISTINCT s2.report_date
    FROM submissions s2
    WHERE s2.company_id = @company_id AND s2.filing_type = 1
    ORDER BY s2.report_date DESC
    LIMIT 5
  )
ORDER BY s.report_date DESC, tc.name
```

Constructor takes `ulong companyId` and `string[] conceptNames`. Returns `IReadOnlyCollection<ScoringConceptValue>`.

Follow the `GetDataPointsForSubmissionStmt` pattern:
- Static ordinal indices (`_conceptNameIndex`, `_valueIndex`, `_reportDateIndex`)
- `BeforeRowProcessing` caches ordinals
- `ProcessCurrentRow` creates `ScoringConceptValue` and adds to list
- `ClearResults` resets the list

**File: `dotnet/Stocks.Persistence/Database/IDbmService.cs`** — add method:
```csharp
Task<Result<IReadOnlyCollection<ScoringConceptValue>>> GetScoringDataPoints(
    ulong companyId, string[] conceptNames, CancellationToken ct);
```

**File: `dotnet/Stocks.Persistence/Database/DbmService.cs`** — implement using `GetScoringDataPointsStmt`.

**File: `dotnet/Stocks.Persistence/Database/DbmInMemoryData.cs`** — add `GetScoringDataPoints` method:
- Filter `_dataPoints` by company_id
- Join with `_submissions` for filing_type=1 and end_date = report_date
- Join with `_taxonomyConcepts` for concept name matching
- Return top 5 distinct report years
- Build `ScoringConceptValue` list

**File: `dotnet/Stocks.Persistence/Database/DbmInMemoryService.cs`** — delegate to `_data.GetScoringDataPoints(...)`.

#### Test

**File: `dotnet/Stocks.EDGARScraper.Tests/Scoring/GetScoringDataPointsTests.cs`**

Use `DbmInMemoryService` + `DbmInMemoryData` with pre-populated test data:

- `GetScoringDataPoints_ReturnsConceptsFromTenKFilings` — insert data points for a company with 10-K submissions, verify correct concepts returned
- `GetScoringDataPoints_ExcludesTenQFilings` — insert both 10-K and 10-Q data, verify only 10-K values returned
- `GetScoringDataPoints_LimitsToFiveMostRecentYears` — insert 7 years of data, verify only 5 most recent returned
- `GetScoringDataPoints_FiltersToRequestedConcepts` — insert many concepts, verify only requested ones returned
- `GetScoringDataPoints_ReturnsEmptyForCompanyWithNoData` — verify empty result for unknown company

#### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

### 2.3. Checkpoints - Checkpoint 3: Scoring Computation Service

Implement the scoring logic as pure functions in a service class. This is the core of the feature.

#### Build

**File: `dotnet/Stocks.Persistence/Services/ScoringService.cs`**

Constructor injects `IDbmService`.

**Public method:**
```csharp
public async Task<Result<ScoringResult>> ComputeScore(
    ulong companyId, CancellationToken ct)
```

This method:
1. Calls `_dbmService.GetScoringDataPoints(companyId, AllConceptNames, ct)` to get raw data
2. Calls `_dbmService.GetCompanyById(companyId, ct)` to get company info (for tickers)
3. Calls `_dbmService.GetPricesByTicker(ticker, ct)` to get latest price (find max price_date, use close — same pattern as `CompanyEndpoints.cs`)
4. Groups raw data by report year → builds `RawDataByYear` dictionary
5. Extracts `sharesOutstanding` from the most recent year's `CommonStockSharesOutstanding` value, and `currentDividendsPaid` from the most recent year's dividends fallback chain
6. Calls `ComputeDerivedMetrics(...)` to compute intermediate values
7. Calls `EvaluateChecks(...)` to run the 13 checks
8. Packages into `ScoringResult`

**Note:** The endpoint (checkpoint 4) already looks up the company by CIK. Passing the company object to the service would avoid a redundant `GetCompanyById` call. However, following the existing `StatementDataService` pattern (services fetch their own dependencies by ID), the service takes `companyId` and fetches what it needs internally.

**Static constant:** `AllConceptNames` — the 33 concept names listed in section 1 of this plan.

**Internal static pure functions** (testable without DB):

```csharp
internal static decimal? ResolveField(
    IReadOnlyDictionary<string, decimal> yearData,
    string[] fallbackChain,
    decimal? defaultValue)
```
Tries each concept name in order, returns first found value or default.

```csharp
internal static DerivedMetrics ComputeDerivedMetrics(
    IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> rawDataByYear,
    decimal? pricePerShare,
    long? sharesOutstanding)
```
Implements all formulas from research.md section 1. The function applies `ResolveField` per year for each fallback chain:
- For each year, resolve fields using fallback chains (each missing optional field defaults to $0)
- **Book Value** = Equity - (Goodwill + Intangibles), using most recent year. Goodwill/Intangibles default to $0.
- **Adjusted Retained Earnings** = CurrentRetainedEarnings + TotalDividends - TotalStockIssuance - TotalPreferredIssuance (totals summed across all years)
- **Oldest Retained Earnings** = RetainedEarnings from the oldest year
- **Per-year Net Cash Flow** = GrossCashFlow - (NetDebtIssuance + NetStockIssuance + NetPreferredIssuance)
- **Per-year Owner Earnings** = NetIncome + Depletion + Amortization + DeferredTax + OtherNonCash - CapEx + ChangeInWorkingCapital (simplified formula — Depreciation cancels out)
- **Averages** = Sum / NumYears
- **Market Cap** = Shares × Price (shares from most recent year's `CommonStockSharesOutstanding` data point)
- **Current Dividends** = most recent year's resolved dividends (used in Estimated Return formula)
- **Estimated Returns** = 100 × (Average - CurrentDividends) / MarketCap

```csharp
internal static IReadOnlyList<ScoringCheck> EvaluateChecks(
    DerivedMetrics metrics, int yearsOfData)
```
Evaluates the 13 checks per research.md section 1.4. Returns `ScoringCheckResult.NotAvailable` when a required metric is null.

#### Test

**File: `dotnet/Stocks.EDGARScraper.Tests/Scoring/ScoringServiceTests.cs`**

**Fallback resolution tests:**
- `ResolveField_ReturnsPrimaryWhenPresent` — primary concept exists, returns its value
- `ResolveField_ReturnsFallbackWhenPrimaryMissing` — only fallback concept exists
- `ResolveField_ReturnsDefaultWhenAllMissing` — no concepts found, returns default
- `ResolveField_ReturnsNullDefaultWhenAllMissing` — no concepts, no default

**Derived metrics tests (with synthetic data):**
- `ComputeDerivedMetrics_BookValue_SubtractsGoodwillAndIntangibles` — equity 500M, goodwill 100M, intangibles 50M → book value 350M
- `ComputeDerivedMetrics_BookValue_DefaultsGoodwillToZero` — equity 500M, no goodwill/intangibles → book value 500M
- `ComputeDerivedMetrics_AdjustedRetainedEarnings_IncludesDividendsAndIssuance` — verify formula across multiple years
- `ComputeDerivedMetrics_NetCashFlow_SubtractsFinancingFromGross` — verify per-year NCF formula
- `ComputeDerivedMetrics_OwnerEarnings_SimplifiedFormula` — verify OE = NetIncome + Depletion + Amortization + DeferredTax + OtherNonCash - CapEx + WorkingCapitalChange
- `ComputeDerivedMetrics_Averages_DivideBySumOfYears` — 3 years of data, verify averages
- `ComputeDerivedMetrics_EstimatedReturn_Formula` — verify EstReturn = 100 × (Average - Dividends) / MarketCap
- `ComputeDerivedMetrics_ReturnsNullMetrics_WhenEquityMissing` — no equity data → BookValue null, ratios null

**13 checks tests:**
- `EvaluateChecks_AllPass_WhenMetricsAreGood` — synthetic metrics that pass all 13 → score 13/13
- `EvaluateChecks_DebtToEquity_FailsAboveThreshold` — ratio 0.6 → fail
- `EvaluateChecks_BookValue_FailsBelowThreshold` — book value 100M → fail
- `EvaluateChecks_NotAvailable_WhenMetricIsNull` — null metric → N/A, not counted in computable
- `EvaluateChecks_HistoryCheck_FailsWithLessThanFourYears` — 3 years → fail
- `EvaluateChecks_RetainedEarningsIncreased_ComparesCurrentToOldest` — verify comparison

**Integration test (with in-memory DB):**
- `ComputeScore_EndToEnd_WithInMemoryData` — populate `DbmInMemoryData` with realistic multi-year data for a test company, call `ComputeScore`, verify scorecard has 13 checks with plausible results

#### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

### 2.4. Checkpoints - Checkpoint 4: API Endpoint

Expose the scoring service via a REST endpoint.

#### Build

**File: `dotnet/Stocks.WebApi/Endpoints/ScoringEndpoints.cs`**

Static class with `MapScoringEndpoints(this IEndpointRouteBuilder app)` extension method.

**Endpoint:** `GET /api/companies/{cik}/scoring`

Handler:
1. Look up company by CIK via `IDbmService.GetCompanyByCik(cik, ct)` (or equivalent — check existing endpoint patterns in `CompanyEndpoints.cs`)
2. Call `ScoringService.ComputeScore(companyId, ct)`
3. Map `ScoringResult` to anonymous JSON response object:

```csharp
new {
    rawDataByYear = ...,  // Dictionary<string, Dictionary<string, decimal>>
    metrics = new {
        bookValue = result.Metrics.BookValue,
        marketCap = result.Metrics.MarketCap,
        // ... all DerivedMetrics fields
    },
    scorecard = ...,  // array of { check, name, value, threshold, result }
    overallScore = result.OverallScore,
    computableChecks = result.ComputableChecks,
    yearsOfData = result.YearsOfData,
    pricePerShare = result.PricePerShare,
    priceDate = result.PriceDate?.ToString("yyyy-MM-dd"),
    sharesOutstanding = result.SharesOutstanding
}
```

**File: `dotnet/Stocks.WebApi/Program.cs`** — register `ScoringService` in DI and call `app.MapScoringEndpoints()`.

#### Test

**File: `dotnet/Stocks.WebApi.Tests/ScoringEndpointsTests.cs`**

Use the existing WebApi test infrastructure (WebApplicationFactory or similar pattern from existing endpoint tests):

- `GetScoring_ReturnsOk_WithValidCik` — verify 200 response with expected JSON shape
- `GetScoring_ReturnsNotFound_WithInvalidCik` — verify 404 for nonexistent company
- `GetScoring_ResponseContainsScorecard_With13Checks` — verify scorecard array has 13 elements
- `GetScoring_ResponseContainsDerivedMetrics` — verify metrics object present with expected fields

#### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

### 2.5. Checkpoints - Checkpoint 5: Frontend Scoring Page

Create the Angular scoring component and link it from the company landing page.

#### Build

**File: `frontend/stocks-frontend/src/app/features/scoring/scoring.component.ts`**

Standalone component with:
- Route: `/company/:cik/scoring`
- Signals: `scoring` (ScoringResponse | null), `company` (CompanyDetail | null), `loading` (boolean), `error` (string | null)
- OnInit: fetch company details + scoring data in parallel

Template sections:
1. **Company header** — reuse pattern from report component (name, CIK, price, tickers)
2. **Score summary** — large badge showing `overallScore / computableChecks` with color coding (green ≥ 10, yellow ≥ 7, red < 7)
3. **Scorecard table** — 13 rows: check name, computed value (formatted), threshold, pass/fail/N/A indicator (green checkmark, red X, gray dash)
4. **Derived metrics** — key-value pairs showing Book Value, Market Cap, D/E Ratio, etc. with formatting (currency for dollar amounts, percentage for returns, ratio for ratios)
5. **Raw data table** — concept names as rows, years as columns, values formatted as currency. Group by category (Balance Sheet, Cash Flow, Income Statement)

**File: `frontend/stocks-frontend/src/app/core/services/api.service.ts`**

Add interfaces:
```typescript
interface ScoringCheckResponse {
  checkNumber: number;
  name: string;
  computedValue: number | null;
  threshold: string;
  result: 'pass' | 'fail' | 'na';
}

interface DerivedMetricsResponse {
  bookValue: number | null;
  marketCap: number | null;
  debtToEquityRatio: number | null;
  priceToBookRatio: number | null;
  debtToBookRatio: number | null;
  adjustedRetainedEarnings: number | null;
  oldestRetainedEarnings: number | null;
  averageNetCashFlow: number | null;
  averageOwnerEarnings: number | null;
  estimatedReturnCF: number | null;
  estimatedReturnOE: number | null;
  currentDividendsPaid: number | null;
}

interface ScoringResponse {
  rawDataByYear: Record<string, Record<string, number>>;
  metrics: DerivedMetricsResponse;
  scorecard: ScoringCheckResponse[];
  overallScore: number;
  computableChecks: number;
  yearsOfData: number;
  pricePerShare: number | null;
  priceDate: string | null;
  sharesOutstanding: number | null;
}
```

Add method:
```typescript
getScoring(cik: string): Observable<ScoringResponse> {
  return this.http.get<ScoringResponse>(`${this.baseUrl}/companies/${cik}/scoring`);
}
```

**File: `frontend/stocks-frontend/src/app/app.routes.ts`** — add route:
```typescript
{ path: 'company/:cik/scoring', loadComponent: () => import('./features/scoring/scoring.component').then(m => m.ScoringComponent) }
```

**File: `frontend/stocks-frontend/src/app/features/company/company.component.ts`** — add "Value Score" link in the company header area, linking to `/company/:cik/scoring`.

#### Test

**File: `frontend/stocks-frontend/src/app/features/scoring/scoring.component.spec.ts`**

- `should create` — basic component creation
- `should fetch scoring data on init` — verify API call made with correct CIK
- `should display score summary` — mock API response, verify score badge renders
- `should display scorecard table with 13 rows` — verify all checks rendered
- `should show pass/fail/na indicators` — verify correct icons/colors per check result
- `should handle error state` — mock API error, verify error message displayed

**File: `frontend/stocks-frontend/src/app/features/company/company.component.spec.ts`** — update existing tests to account for the new "Value Score" link.

#### Verify

```bash
cd frontend/stocks-frontend && npx ng test --no-watch
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

---

## Metadata

### Status
success

### Dependencies
- `.prompts/company-scoring-system-research/research.md` — formulas, concept mappings, data availability stats
- `dotnet/Stocks.Persistence/Database/IDbmService.cs` — interface to extend
- `dotnet/Stocks.Persistence/Database/DbmInMemoryData.cs` — in-memory backing to extend
- `dotnet/Stocks.Persistence/Services/StatementDataService.cs` — service pattern to follow
- `dotnet/Stocks.WebApi/Endpoints/CompanyEndpoints.cs` — endpoint pattern to follow
- `frontend/stocks-frontend/src/app/features/report/report.component.ts` — company header pattern to reuse
- `prices` table — for stock price / market cap
- `data_points` + `taxonomy_concepts` + `submissions` tables — for financial data

### Open Questions
- Sign conventions should be verified against sample data (Apple CIK 320193) during implementation of checkpoint 3. See research.md section 2.2.
- Should `CommonStockSharesOutstanding` come from the most recent 10-K's data_points, or is there a better source?
- The `ChangeInWorkingCapital` balance-sheet-diff fallback requires comparing consecutive years' `AssetsCurrent` and `LiabilitiesCurrent`. This adds complexity — consider implementing only the direct concepts initially and adding the fallback later if coverage is insufficient.

### Assumptions
- The `dp.end_date = s.report_date` filter correctly selects the right period's data point for both instant and duration concepts in 10-K filings. If multiple data points exist for the same concept/year (e.g., different dimensions or units), take the first value encountered.
- Each data point references a specific `taxonomy_concept_id`, so joining through `tc.name` won't produce cross-taxonomy-version duplicates from the taxonomy side.
- Missing optional concepts (debt, goodwill, intangibles, dividends, etc.) genuinely represent $0 values and can be safely defaulted.
- The existing `GetPricesByTicker` method returns enough data to find the most recent price. If it returns all prices, we select the max `price_date`.
- `ScoringService` will be registered as a scoped or transient service in DI.
