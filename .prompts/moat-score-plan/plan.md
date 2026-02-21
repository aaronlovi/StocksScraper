# Plan: Moat Score Implementation

## Table of Contents

- [Metadata](#metadata)
- [Checkpoint 1: Data Models](#checkpoint-1-data-models)
- [Checkpoint 2: Database Layer](#checkpoint-2-database-layer)
- [Checkpoint 3: MoatScoringService — Concept Resolution and Derived Metrics](#checkpoint-3-moatscoringservice--concept-resolution-and-derived-metrics)
- [Checkpoint 4: MoatScoringService — Check Evaluation and Single-Company Scoring](#checkpoint-4-moatscoringservice--check-evaluation-and-single-company-scoring)
- [Checkpoint 5: Batch Scoring and CLI Command](#checkpoint-5-batch-scoring-and-cli-command)
- [Checkpoint 6: Backend API Endpoints](#checkpoint-6-backend-api-endpoints)
- [Checkpoint 7: Frontend — API Service and Moat Detail Page](#checkpoint-7-frontend--api-service-and-moat-detail-page)
- [Checkpoint 8: Frontend — Moat List Page and Navigation](#checkpoint-8-frontend--moat-list-page-and-navigation)

---

## Metadata

- **Status:** complete
- **Dependencies:**
  - `.prompts/moat-score-research/research.md` — codebase research findings
  - `docs/moat-score-design.md` — authoritative check definitions and thresholds
  - `dotnet/Stocks.Persistence/Services/ScoringService.cs` — shared static helpers (`ResolveField`, `ResolveEquity`, `GroupAndPartitionData`, `ComputeDerivedMetrics`)
  - `dotnet/Stocks.DataModels/Scoring/` — shared model types (`ScoringCheck`, `ScoringCheckResult`, `ScoringConceptValue`, `BatchScoringConceptValue`, `LatestPrice`)
- **Open Questions:** None
- **Assumptions:**
  - Moat Score is fully independent from Value Score: separate service, table, endpoints, components.
  - Revenue fallback chain matches `CompanyEndpoints.RevenueFallbackChain` (4 concepts: `Revenues`, `RevenueFromContractWithCustomerExcludingAssessedTax`, `SalesRevenueNet`, `RevenueFromContractWithCustomerIncludingAssessedTax`).
  - Year limit in data queries is parameterized (from hardcoded 5 to a constructor parameter) so Moat can request 8 years without duplicating SQL.
  - Financial companies receiving `NotAvailable` for margin checks is acceptable.
  - Frontend has no automated test suite — frontend checkpoints are verified by compilation and visual inspection.

---

## Checkpoint 1: Data Models

**Goal:** Create all new record types needed by the Moat Score system — derived metrics, per-year trend data, scoring result, summary for the list page, and report request enums.

### Build

**Create `dotnet/Stocks.DataModels/Scoring/MoatDerivedMetrics.cs`:**

```csharp
public record MoatDerivedMetrics(
    decimal? AverageGrossMargin,       // avg gross margin % across years
    decimal? AverageOperatingMargin,   // avg operating margin % across years
    decimal? AverageRoeCF,             // avg ROE (cash flow) % across years
    decimal? AverageRoeOE,             // avg ROE (owner earnings) % across years
    decimal? RevenueCagr,              // compound annual growth rate %
    decimal? CapexRatio,               // avg CapEx / avg OE * 100
    decimal? InterestCoverage,         // operating income / interest expense (most recent year)
    decimal? DebtToEquityRatio,        // debt / equity
    decimal? EstimatedReturnOE,        // (avgOE - dividends) / marketCap * 100
    decimal? CurrentDividendsPaid,     // most recent year dividends
    decimal? MarketCap,                // price * shares outstanding
    decimal? PricePerShare,            // latest close price
    int PositiveOeYears,               // count of years with OE > 0
    int TotalOeYears,                  // count of years with computable OE
    int CapitalReturnYears,            // count of years with dividends + buybacks > 0
    int TotalCapitalReturnYears        // count of years checked for capital return
);
```

**Create `dotnet/Stocks.DataModels/Scoring/MoatYearMetrics.cs`:**

Per-year trend data for the detail page's 6 charts (AR/Revenue is fetched separately via existing endpoint):

```csharp
public record MoatYearMetrics(
    int Year,
    decimal? GrossMarginPct,
    decimal? OperatingMarginPct,
    decimal? RoeCfPct,
    decimal? RoeOePct,
    decimal? Revenue
);
```

**Create `dotnet/Stocks.DataModels/Scoring/MoatScoringResult.cs`:**

```csharp
public record MoatScoringResult(
    IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> RawDataByYear,
    MoatDerivedMetrics Metrics,
    IReadOnlyList<ScoringCheck> Scorecard,
    IReadOnlyList<MoatYearMetrics> TrendData,
    int OverallScore,
    int ComputableChecks,
    int YearsOfData,
    decimal? PricePerShare,
    DateOnly? PriceDate,
    long? SharesOutstanding
);
```

**Create `dotnet/Stocks.DataModels/Scoring/CompanyMoatScoreSummary.cs`:**

Summary record for the `company_moat_scores` table and list page:

```csharp
public record CompanyMoatScoreSummary(
    ulong CompanyId,
    string Cik,
    string? CompanyName,
    string? Ticker,
    string? Exchange,
    int OverallScore,
    int ComputableChecks,
    int YearsOfData,
    decimal? AverageGrossMargin,
    decimal? AverageOperatingMargin,
    decimal? AverageRoeCF,
    decimal? AverageRoeOE,
    decimal? EstimatedReturnOE,
    decimal? RevenueCagr,
    decimal? CapexRatio,
    decimal? InterestCoverage,
    decimal? DebtToEquityRatio,
    decimal? PricePerShare,
    DateOnly? PriceDate,
    long? SharesOutstanding,
    DateTime ComputedAt
);
```

**Create `dotnet/Stocks.DataModels/Scoring/MoatScoresReportRequest.cs`:**

Sort and filter types for the Moat list endpoint:

```csharp
public enum MoatScoresSortBy
{
    OverallScore,
    AverageGrossMargin,
    AverageOperatingMargin,
    AverageRoeCF,
    AverageRoeOE,
    EstimatedReturnOE,
    RevenueCagr,
    CapexRatio,
    InterestCoverage,
    DebtToEquityRatio
}
```

Reuse existing `SortDirection` and `ScoresFilter` from `ScoresReportRequest.cs` — they already have `MinScore`, `MaxScore`, and `Exchange`.

### Test

**Create `dotnet/Stocks.EDGARScraper.Tests/Scoring/MoatScoringModelTests.cs`:**

- `MoatDerivedMetrics_AllNulls_CanBeConstructed`: Construct `MoatDerivedMetrics` with all-null fields, assert no exception.
- `MoatScoringResult_CountsPassingChecks`: Build a `MoatScoringResult` with a mix of Pass/Fail/NotAvailable checks, verify `OverallScore` and `ComputableChecks` match expected values. (Note: the counting logic is in the service, not the model — this test verifies the model holds the correct values as passed.)
- `CompanyMoatScoreSummary_CanBeConstructed`: Construct with sample data, verify all fields are accessible.
- `MoatYearMetrics_CanBeConstructed`: Construct with sample year data.

### Files

| Action | File |
|--------|------|
| Create | `dotnet/Stocks.DataModels/Scoring/MoatDerivedMetrics.cs` |
| Create | `dotnet/Stocks.DataModels/Scoring/MoatYearMetrics.cs` |
| Create | `dotnet/Stocks.DataModels/Scoring/MoatScoringResult.cs` |
| Create | `dotnet/Stocks.DataModels/Scoring/CompanyMoatScoreSummary.cs` |
| Create | `dotnet/Stocks.DataModels/Scoring/MoatScoresReportRequest.cs` |
| Create | `dotnet/Stocks.EDGARScraper.Tests/Scoring/MoatScoringModelTests.cs` |

### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln --filter "FullyQualifiedName~MoatScoringModelTests"
```

---

## Checkpoint 2: Database Layer

**Goal:** Create the `company_moat_scores` table, parameterize the year limit in existing scoring data point statements, add Moat-specific database methods to the service interfaces, and implement them in both the real and in-memory services.

### Build

**Create `dotnet/Stocks.Persistence/Database/Migrations/V017__AddCompanyMoatScoresTable.sql`:**

```sql
CREATE TABLE company_moat_scores (
    company_id bigint PRIMARY KEY,
    cik bigint NOT NULL,
    company_name varchar(200),
    ticker varchar(20),
    exchange varchar(50),
    overall_score int NOT NULL,
    computable_checks int NOT NULL,
    years_of_data int NOT NULL,
    average_gross_margin decimal,
    average_operating_margin decimal,
    average_roe_cf decimal,
    average_roe_oe decimal,
    estimated_return_oe decimal,
    revenue_cagr decimal,
    capex_ratio decimal,
    interest_coverage decimal,
    debt_to_equity_ratio decimal,
    price_per_share decimal,
    price_date date,
    shares_outstanding bigint,
    computed_at timestamptz NOT NULL DEFAULT NOW()
);
```

**Modify `dotnet/Stocks.Persistence/Database/Statements/GetScoringDataPointsStmt.cs`:**

Add a `yearLimit` parameter to the constructor (default 5). Replace hardcoded `LIMIT 5` in the SQL with `LIMIT @year_limit`, and add `@year_limit` as an `int` parameter. The existing constructor signature stays as-is (using `this(companyId, conceptNames, 5)` delegation).

**Modify `dotnet/Stocks.Persistence/Database/Statements/GetAllScoringDataPointsStmt.cs`:**

Same pattern: add `yearLimit` parameter (default 5). Replace hardcoded `rn <= 5` with `rn <= @year_limit`, and add `@year_limit` as an `int` parameter.

**Create `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyMoatScoresStmt.cs`:**

Follow the pattern of `BulkInsertCompanyScoresStmt.cs`: `BulkInsertDbStmtBase<CompanyMoatScoreSummary>`, `COPY company_moat_scores (...) FROM STDIN (FORMAT BINARY)`, write all 21 columns in order.

**Create `dotnet/Stocks.Persistence/Database/Statements/TruncateCompanyMoatScoresStmt.cs`:**

Follow existing truncate pattern: `TRUNCATE company_moat_scores`.

**Create `dotnet/Stocks.Persistence/Database/Statements/GetCompanyMoatScoresStmt.cs`:**

Follow the pattern of `GetCompanyScoresStmt.cs`: paginated query against `company_moat_scores` with sorting by `MoatScoresSortBy` enum columns and filtering by `ScoresFilter`. Return `PagedResults<CompanyMoatScoreSummary>`.

**Modify `dotnet/Stocks.Persistence/Database/IDbmService.cs`:**

Add 5 new methods:

```csharp
Task<Result<IReadOnlyCollection<ScoringConceptValue>>> GetScoringDataPoints(
    ulong companyId, string[] conceptNames, int yearLimit, CancellationToken ct);

Task<Result<IReadOnlyCollection<BatchScoringConceptValue>>> GetAllScoringDataPoints(
    string[] conceptNames, int yearLimit, CancellationToken ct);

Task<Result> TruncateCompanyMoatScores(CancellationToken ct);

Task<Result> BulkInsertCompanyMoatScores(List<CompanyMoatScoreSummary> scores, CancellationToken ct);

Task<Result<PagedResults<CompanyMoatScoreSummary>>> GetCompanyMoatScores(
    PaginationRequest pagination, MoatScoresSortBy sortBy, SortDirection sortDir,
    ScoresFilter? filter, CancellationToken ct);
```

The first two are overloads of existing methods with an added `yearLimit` parameter. The existing 2-arg methods remain unchanged (they internally pass yearLimit=5).

**Modify `dotnet/Stocks.Persistence/Database/DbmService.cs`:**

Implement the 5 new `IDbmService` methods. The `GetScoringDataPoints` overload creates `new GetScoringDataPointsStmt(companyId, conceptNames, yearLimit)`. Same for `GetAllScoringDataPoints`.

**Modify `dotnet/Stocks.Persistence/Database/DbmInMemoryService.cs`:**

Implement the 5 new methods. The year-limit overloads delegate to `_data` methods that accept `yearLimit`. The moat score methods delegate to new `_data` methods: `TruncateCompanyMoatScores()`, `AddCompanyMoatScores(scores)`, `GetCompanyMoatScoresPaged(...)`.

**Modify `dotnet/Stocks.Persistence/Database/DbmInMemoryData.cs`:**

Add backing storage (`List<CompanyMoatScoreSummary>`) and implement `TruncateCompanyMoatScores`, `AddCompanyMoatScores`, `GetCompanyMoatScoresPaged`. Update `GetScoringDataPoints` and `GetAllScoringDataPoints` to accept optional `yearLimit` parameter (existing callers pass 5 by default).

### Test

**Create `dotnet/Stocks.EDGARScraper.Tests/Scoring/GetMoatScoringDataPointsTests.cs`:**

- `GetScoringDataPoints_WithYearLimit8_Returns8Years`: Seed a company with 10 years of annual data and verify that calling `GetScoringDataPoints(companyId, concepts, 8, ct)` returns data for 8 years (not 5).
- `GetScoringDataPoints_WithYearLimit5_Returns5Years`: Same setup, verify yearLimit=5 still works correctly (regression).
- `GetAllScoringDataPoints_WithYearLimit8_Returns8YearsPerCompany`: Seed multiple companies, verify batch query respects the 8-year limit.

These tests use `DbmInMemoryService` following the pattern in `GetScoringDataPointsTests.cs`.

### Files

| Action | File |
|--------|------|
| Create | `dotnet/Stocks.Persistence/Database/Migrations/V017__AddCompanyMoatScoresTable.sql` |
| Modify | `dotnet/Stocks.Persistence/Database/Statements/GetScoringDataPointsStmt.cs` |
| Modify | `dotnet/Stocks.Persistence/Database/Statements/GetAllScoringDataPointsStmt.cs` |
| Create | `dotnet/Stocks.Persistence/Database/Statements/BulkInsertCompanyMoatScoresStmt.cs` |
| Create | `dotnet/Stocks.Persistence/Database/Statements/TruncateCompanyMoatScoresStmt.cs` |
| Create | `dotnet/Stocks.Persistence/Database/Statements/GetCompanyMoatScoresStmt.cs` |
| Modify | `dotnet/Stocks.Persistence/Database/IDbmService.cs` |
| Modify | `dotnet/Stocks.Persistence/Database/DbmService.cs` |
| Modify | `dotnet/Stocks.Persistence/Database/DbmInMemoryService.cs` |
| Modify | `dotnet/Stocks.Persistence/Database/DbmInMemoryData.cs` |
| Create | `dotnet/Stocks.EDGARScraper.Tests/Scoring/GetMoatScoringDataPointsTests.cs` |

### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln --filter "FullyQualifiedName~GetMoatScoringDataPointsTests"
dotnet test dotnet/EDGARScraper.sln --filter "FullyQualifiedName~GetScoringDataPointsTests"
```

Run existing `GetScoringDataPointsTests` to verify no regressions.

---

## Checkpoint 3: MoatScoringService — Concept Resolution and Derived Metrics

**Goal:** Create `MoatScoringService` with concept name arrays, fallback chains, and the `ComputeMoatDerivedMetrics` static method that computes all Moat-specific metrics (gross margin, operating margin, revenue CAGR, capex ratio, interest coverage, positive OE tracking, capital return tracking) plus per-year trend data.

### Build

**Modify `dotnet/Stocks.Persistence/Services/ScoringService.cs`:**

Change visibility from `private static` to `internal static` for:
- `MakeCheck` (line 1055) — so `MoatScoringService` can build `ScoringCheck` records.
- Fallback chain arrays needed by `MoatScoringService`: `NetIncomeChain`, `CapExChain`, `DividendsChain`, `StockRepurchaseChain`, `DebtChain`, and the NCF-related chains (`GrossCashFlowChain`, etc.). Verify the exact set during implementation by checking which chains are referenced in the per-year OE and NCF computations within `ComputeDerivedMetrics`.

**Create `dotnet/Stocks.Persistence/Services/MoatScoringService.cs`:**

This is the core of the Moat Score system. The class follows the same structure as `ScoringService` but with Moat-specific logic.

**Concept arrays (static readonly):**

New Moat-specific fallback chains:

```csharp
internal static readonly string[] RevenueChain = [
    "Revenues",
    "RevenueFromContractWithCustomerExcludingAssessedTax",
    "SalesRevenueNet",
    "RevenueFromContractWithCustomerIncludingAssessedTax",
];

internal static readonly string[] CostOfGoodsChain = [
    "CostOfGoodsAndServicesSold",
    "CostOfRevenue",
    "CostOfGoodsSold",
];

internal static readonly string[] GrossProfitChain = [
    "GrossProfit",
];

internal static readonly string[] OperatingIncomeChain = [
    "OperatingIncomeLoss",
];

internal static readonly string[] InterestExpenseChain = [
    "InterestExpense",
    "InterestExpenseDebt",
];
```

**Existing ScoringService chains to make `internal`:**

The per-year loop needs several fallback chains that are currently `private static` in `ScoringService`. Change them to `internal static` (same low-risk pattern as the `MakeCheck` visibility change — same assembly, no behavioral impact):

- `NetIncomeChain` — for per-year OE computation (net income component)
- `CapExChain` — for per-year OE and CapEx ratio
- `DividendsChain` — for capital return tracking and estimated return
- `StockRepurchaseChain` — for capital return tracking
- `DebtChain` — for debt-to-equity computation
- Cash flow chains for NCF computation (`GrossCashFlowChain` and related issuance chains: `NetDebtIssuanceChain`, `NetStockIssuanceChain`, `NetPreferredIssuanceChain` — verify exact names during implementation)

Note: The existing `internal static` resolve helpers (`ResolveEquity`, `ResolveDepletionAndAmortization`, `ResolveDeferredTax`, `ResolveOtherNonCash`, `ResolveWorkingCapitalChange`) encapsulate their own chains internally, so those chains do NOT need visibility changes.

`MoatConceptNames`: a combined array of all concepts from `ScoringService.AllConceptNames` plus the new Moat-specific concepts above (deduplicated). Approximately 153 total strings.

**`ComputeMoatDerivedMetrics` method (internal static):**

Signature:
```csharp
internal static (MoatDerivedMetrics Metrics, IReadOnlyList<MoatYearMetrics> TrendData)
    ComputeMoatDerivedMetrics(
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> annualDataByYear,
        IReadOnlyDictionary<string, decimal> mostRecentSnapshot,
        decimal? oldestRetainedEarnings,
        decimal? pricePerShare,
        long? sharesOutstanding,
        IReadOnlyDictionary<string, TaxonomyBalanceTypes>? balanceTypes = null)
```

Computes everything in a single pass — does NOT call `ScoringService.ComputeDerivedMetrics`. This avoids double iteration and gives full control over per-year values needed for trend data. Uses `ScoringService`'s `internal static` helpers (`ResolveField`, `ResolveEquity`, `ResolveDepletionAndAmortization`, `ResolveDeferredTax`, `ResolveOtherNonCash`, `ResolveWorkingCapitalChange`) directly.

**Per-year loop over sorted annual years:**

For each year in `annualDataByYear` (sorted ascending):
1. **Revenue:** `ScoringService.ResolveField(yearData, RevenueChain, null)`
2. **Gross profit:** `ScoringService.ResolveField(yearData, GrossProfitChain, null)` — if null, derive from `revenue - cogs` where `cogs = ScoringService.ResolveField(yearData, CostOfGoodsChain, null)`.
3. **Gross margin %:** `grossProfit / revenue * 100` (if both non-null and revenue != 0).
4. **Operating income:** `ScoringService.ResolveField(yearData, OperatingIncomeChain, null)`
5. **Operating margin %:** `operatingIncome / revenue * 100` (if both non-null and revenue != 0).
6. **Equity:** `ScoringService.ResolveEquity(yearData)` — needed for ROE and D/E.
7. **Net cash flow per year:** Same computation as `ScoringService.ComputeDerivedMetrics` — gross cash flow - net debt issuance - net stock issuance - net preferred issuance. Then `roeCfPct = 100 * ncf / equity`.
8. **Owner earnings per year:** `netIncome + depletionAndAmortization + deferredTax + otherNonCash - capEx + workingCapitalChange` (using the same resolve helpers from `ScoringService`). Then `roeOePct = 100 * oe / equity`. Track sign for "positive OE every year" check: increment `positiveOeYears` if OE > 0.
9. **CapEx per year:** `ResolveField(yearData, CapExChain, 0m)`. Accumulate for average.
10. **Dividends + buybacks per year:** `dividends = ResolveField(yearData, DividendsChain, 0m)`, `buybacks = ResolveField(yearData, StockRepurchaseChain, 0m)`. If `dividends + buybacks > 0`, increment `capitalReturnYears`.
11. **Build `MoatYearMetrics`** for this year: `(year, grossMarginPct, operatingMarginPct, roeCfPct, roeOePct, revenue)`.

**After the loop:**
1. **Average gross margin:** `totalGrossMargin / yearsWithGrossMargin`
2. **Average operating margin:** `totalOperatingMargin / yearsWithOperatingMargin`
3. **Average ROE CF/OE:** `totalRoeCf / yearsWithRoeCf`, `totalRoeOe / yearsWithRoeOe`
4. **Revenue CAGR:** `(Math.Pow((double)(latestRevenue / oldestRevenue), 1.0 / yearsBetween) - 1) * 100` — uses oldest and latest years with revenue data. Needs >= 2 years.
5. **CapEx ratio:** `(avgCapEx / avgOE) * 100` — uses accumulated totals from the loop.
6. **Interest coverage:** Most recent fiscal year only: `operatingIncome / interestExpense` from the latest year in `annualDataByYear`.
7. **Debt-to-equity:** From `mostRecentSnapshot` (same logic as Value Score — uses `ScoringService.ResolveEquity` and `ScoringService.ResolveField` with debt chain).
8. **Estimated return OE:** `100 * (avgOE - dividends) / marketCap` (same formula as Value Score).
9. **Market cap:** `pricePerShare * sharesOutstanding`.
10. **Assemble `MoatDerivedMetrics`** from all computed values.

Return tuple `(MoatDerivedMetrics, List<MoatYearMetrics>)`.

### Test

**Create `dotnet/Stocks.EDGARScraper.Tests/Scoring/MoatScoringServiceTests.cs`:**

Test the static `ComputeMoatDerivedMetrics` method directly (same pattern as `ScoringServiceTests.cs`).

**Gross margin tests:**
- `ComputeMoatDerivedMetrics_GrossMargin_UsesGrossProfitDirectly`: Provide `GrossProfit` and `Revenues` for 3 years. Verify `AverageGrossMargin` is correct average.
- `ComputeMoatDerivedMetrics_GrossMargin_DerivesFromRevenueMinusCogs`: Provide `Revenues` and `CostOfGoodsAndServicesSold` (no `GrossProfit`). Verify gross margin derived correctly.
- `ComputeMoatDerivedMetrics_GrossMargin_NotAvailable_WhenNoData`: Provide no revenue/COGS/GP data. Verify `AverageGrossMargin` is null.

**Operating margin tests:**
- `ComputeMoatDerivedMetrics_OperatingMargin_ComputesCorrectly`: Provide `OperatingIncomeLoss` and `Revenues`. Verify average.
- `ComputeMoatDerivedMetrics_OperatingMargin_NullWhenNoOperatingIncome`: No operating income data. Verify null.

**Revenue CAGR tests:**
- `ComputeMoatDerivedMetrics_RevenueCagr_ComputesOverMultipleYears`: Provide revenue for 5 years with known growth. Verify CAGR calculation.
- `ComputeMoatDerivedMetrics_RevenueCagr_NullWhenLessThanTwoYears`: Only 1 year of revenue. Verify null.

**CapEx ratio tests:**
- `ComputeMoatDerivedMetrics_CapexRatio_ComputesCorrectly`: Provide OE and CapEx data. Verify ratio.

**Interest coverage tests:**
- `ComputeMoatDerivedMetrics_InterestCoverage_UsesMostRecentYear`: Provide multiple years, verify only latest year is used.
- `ComputeMoatDerivedMetrics_InterestCoverage_NullWhenNoInterestExpense`: No interest expense. Verify null.

**Positive OE tracking tests:**
- `ComputeMoatDerivedMetrics_PositiveOeYears_CountsCorrectly`: Mix of positive and negative OE years. Verify counts.

**Capital return tracking tests:**
- `ComputeMoatDerivedMetrics_CapitalReturnYears_CountsDividendsAndBuybacks`: Years with varying dividend/buyback combinations. Verify counts.

**Trend data tests:**
- `ComputeMoatDerivedMetrics_TrendData_ReturnsPerYearMetrics`: Verify `TrendData` list has correct entries per year.

### Files

| Action | File |
|--------|------|
| Modify | `dotnet/Stocks.Persistence/Services/ScoringService.cs` (MakeCheck visibility) |
| Create | `dotnet/Stocks.Persistence/Services/MoatScoringService.cs` |
| Create | `dotnet/Stocks.EDGARScraper.Tests/Scoring/MoatScoringServiceTests.cs` |

### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln --filter "FullyQualifiedName~MoatScoringServiceTests"
dotnet test dotnet/EDGARScraper.sln --filter "FullyQualifiedName~ScoringServiceTests"
```

Run existing `ScoringServiceTests` to verify no regressions from `MakeCheck` visibility change.

---

## Checkpoint 4: MoatScoringService — Check Evaluation and Single-Company Scoring

**Goal:** Implement `EvaluateMoatChecks` (all 13 checks from the design doc) and the `ComputeScore` instance method for on-demand single-company Moat Score computation.

### Build

**Add to `dotnet/Stocks.Persistence/Services/MoatScoringService.cs`:**

**`EvaluateMoatChecks` method (internal static):**

```csharp
internal static IReadOnlyList<ScoringCheck> EvaluateMoatChecks(
    MoatDerivedMetrics metrics, int yearsOfData)
```

Creates a `List<ScoringCheck>(13)` with all 13 checks:

| # | Name | Computed Value | Threshold | Pass Condition |
|---|------|----------------|-----------|----------------|
| 1 | High ROE (CF) avg | `metrics.AverageRoeCF` | `">= 15%"` | `value >= 15` |
| 2 | High ROE (OE) avg | `metrics.AverageRoeOE` | `">= 15%"` | `value >= 15` |
| 3 | Gross margin avg | `metrics.AverageGrossMargin` | `">= 40%"` | `value >= 40` |
| 4 | Operating margin avg | `metrics.AverageOperatingMargin` | `">= 15%"` | `value >= 15` |
| 5 | Revenue growth | `metrics.RevenueCagr` | `"> 3%"` | `value > 3` |
| 6 | Positive OE every year | `metrics.PositiveOeYears` / `metrics.TotalOeYears` | `"0 failing years"` | `metrics.PositiveOeYears == metrics.TotalOeYears && metrics.TotalOeYears > 0` |
| 7 | Low capex ratio | `metrics.CapexRatio` | `"< 50%"` | `value < 50` |
| 8 | Consistent dividend/buyback | `metrics.CapitalReturnYears` / `metrics.TotalCapitalReturnYears` | `">= 75% of years"` | `capitalReturnYears >= 0.75 * totalYears && totalYears > 0` |
| 9 | Debt-to-equity | `metrics.DebtToEquityRatio` | `"< 1.0"` | `value < 1.0m` |
| 10 | Interest coverage | `metrics.InterestCoverage` | `"> 5x"` | `value > 5` |
| 11 | History | `yearsOfData` | `">= 7 years"` | `yearsOfData >= 7` |
| 12 | Estimated return (OE) floor | `metrics.EstimatedReturnOE` | `"> 3%"` | `value > 3` |
| 13 | Estimated return (OE) cap | `metrics.EstimatedReturnOE` | `"< 40%"` | `value < 40` |

Each check uses `ScoringService.MakeCheck(...)` (now `internal static`). Computed value is `null` → result is `NotAvailable`. Otherwise, evaluate the condition → `Pass` or `Fail`.

Special cases:
- Check 6 (positive OE): `computedValue` is shown as count of failing years (`totalOeYears - positiveOeYears`). `NotAvailable` if `totalOeYears == 0`.
- Check 8 (consistent return): `computedValue` is shown as percentage of years with return (`capitalReturnYears / totalYears * 100`). `NotAvailable` if `totalYears == 0`.

**`ComputeScore` instance method:**

```csharp
public async Task<Result<MoatScoringResult>> ComputeScore(ulong companyId, CancellationToken ct)
```

Follows `ScoringService.ComputeScore` flow:
1. Fetch scoring data: `_dbmService.GetScoringDataPoints(companyId, MoatConceptNames, 8, ct)` — uses year limit 8.
2. Fetch company, tickers, latest price (same as Value Score).
3. `ScoringService.GroupAndPartitionData(values)` — reuse directly.
4. `ComputeMoatDerivedMetrics(annualByYear, mostRecentSnapshot, oldestRetainedEarnings, pricePerShare, sharesOutstanding, balanceTypes)`.
5. `EvaluateMoatChecks(metrics, yearsOfData)`.
6. Count scores: iterate checks, count `Pass` and `Pass + Fail` (computable).
7. Return `MoatScoringResult(rawDataByYear, metrics, scorecard, trendData, overallScore, computableChecks, yearsOfData, pricePerShare, priceDate, sharesOutstanding)`.

Constructor: `public MoatScoringService(IDbmService dbmService, ILogger logger)` — same pattern as `ScoringService`.

### Test

**Add to `dotnet/Stocks.EDGARScraper.Tests/Scoring/MoatScoringServiceTests.cs`:**

**Check evaluation tests (one per check, testing pass/fail/NA):**

- `EvaluateMoatChecks_Check1_RoeCF_Pass`: avgRoeCF = 20 → Pass
- `EvaluateMoatChecks_Check1_RoeCF_Fail`: avgRoeCF = 10 → Fail
- `EvaluateMoatChecks_Check1_RoeCF_NotAvailable`: avgRoeCF = null → NotAvailable
- `EvaluateMoatChecks_Check3_GrossMargin_Pass`: avgGrossMargin = 45 → Pass (threshold >= 40)
- `EvaluateMoatChecks_Check3_GrossMargin_Fail`: avgGrossMargin = 30 → Fail
- `EvaluateMoatChecks_Check5_RevenueCagr_Pass`: revenueCagr = 5 → Pass (threshold > 3)
- `EvaluateMoatChecks_Check5_RevenueCagr_Fail`: revenueCagr = 2 → Fail
- `EvaluateMoatChecks_Check6_PositiveOe_Pass`: 5 positive out of 5 total → Pass
- `EvaluateMoatChecks_Check6_PositiveOe_Fail`: 4 positive out of 5 total → Fail
- `EvaluateMoatChecks_Check7_CapexRatio_Pass`: capexRatio = 30 → Pass (threshold < 50)
- `EvaluateMoatChecks_Check8_ConsistentReturn_Pass`: 4 out of 5 years (80%) → Pass (threshold >= 75%)
- `EvaluateMoatChecks_Check8_ConsistentReturn_Fail`: 3 out of 5 years (60%) → Fail
- `EvaluateMoatChecks_Check10_InterestCoverage_Pass`: coverage = 8 → Pass (threshold > 5)
- `EvaluateMoatChecks_Check11_History_Pass`: yearsOfData = 7 → Pass
- `EvaluateMoatChecks_Check11_History_Fail`: yearsOfData = 5 → Fail
- `EvaluateMoatChecks_Check12_EstReturnFloor_Pass`: estReturn = 5 → Pass (threshold > 3)
- `EvaluateMoatChecks_Check13_EstReturnCap_Pass`: estReturn = 10 → Pass (threshold < 40)
- `EvaluateMoatChecks_Check13_EstReturnCap_Fail`: estReturn = 50 → Fail
- `EvaluateMoatChecks_ReturnsAll13Checks`: Verify list has exactly 13 items with correct check numbers.

### Files

| Action | File |
|--------|------|
| Modify | `dotnet/Stocks.Persistence/Services/MoatScoringService.cs` |
| Modify | `dotnet/Stocks.EDGARScraper.Tests/Scoring/MoatScoringServiceTests.cs` |

### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln --filter "FullyQualifiedName~MoatScoringServiceTests"
```

---

## Checkpoint 5: Batch Scoring and CLI Command

**Goal:** Implement `ComputeAllMoatScores` for bulk computation across all companies, and wire up the `--compute-all-moat-scores` CLI command in `Program.cs`.

### Build

**Add to `dotnet/Stocks.Persistence/Services/MoatScoringService.cs`:**

**`ComputeAllMoatScores` method:**

```csharp
public async Task<Result<IReadOnlyCollection<CompanyMoatScoreSummary>>> ComputeAllMoatScores(CancellationToken ct)
```

Follows `ScoringService.ComputeAllScores` pattern (lines 198-370):
1. Fetch all scoring data: `_dbmService.GetAllScoringDataPoints(MoatConceptNames, 8, ct)` — year limit 8.
2. Fetch all latest prices via `_dbmService.GetAllLatestPrices(ct)`.
3. Fetch all tickers via `_dbmService.GetTickersByCompanyId(ct)`.
4. Fetch all company names via `_dbmService.GetCompanyNames(ct)`.
5. Fetch all companies via `_dbmService.GetCompanies(ct)`.
6. Build lookup dictionaries (prices by company, tickers by company, etc.).
7. Group `BatchScoringConceptValue` by `CompanyId`.
8. For each company:
   a. Convert `BatchScoringConceptValue` → `ScoringConceptValue` (strip CompanyId).
   b. `ScoringService.GroupAndPartitionData(values)`.
   c. `ComputeMoatDerivedMetrics(...)` — only need metrics, not trend data (discard trend data for bulk).
   d. `EvaluateMoatChecks(metrics, yearsOfData)`.
   e. Count passing/computable checks.
   f. Build `CompanyMoatScoreSummary(...)`.
9. Return all summaries.

**Modify `dotnet/Stocks.EDGARScraper/Program.cs`:**

Add a new case in the CLI switch (after the existing `--compute-all-scores` case):

```csharp
case "--compute-all-moat-scores": {
    Result res = await ComputeAndStoreAllMoatScoresAsync();
    // handle result same as --compute-all-scores
    break;
}
```

Add `ComputeAndStoreAllMoatScoresAsync` method:

```csharp
private static async Task<Result> ComputeAndStoreAllMoatScoresAsync()
{
    var moatService = new MoatScoringService(_dbm!, _logger);
    Result<IReadOnlyCollection<CompanyMoatScoreSummary>> computeResult =
        await moatService.ComputeAllMoatScores(ct);
    // .Then() truncate → bulk insert, same pattern as existing
}
```

### Test

**Create `dotnet/Stocks.EDGARScraper.Tests/Scoring/BatchMoatScoringServiceTests.cs`:**

Follow the pattern of `BatchScoringServiceTests.cs`:

- `ComputeAllMoatScores_SingleCompany_ReturnsScore`: Seed one company with 8 years of annual data (10-K submissions) including revenue, COGS, operating income, interest expense, equity, debt, net income, cash flow, CapEx, dividends, and all standard concepts. Verify returned list has 1 `CompanyMoatScoreSummary` with non-zero score.
- `ComputeAllMoatScores_CompanyWithInsufficientData_Returns0Score`: Seed company with only 1 year of data. Verify score is 0 or very low (most checks NotAvailable or Fail).
- `ComputeAllMoatScores_MultipleCompanies_ReturnsAll`: Seed 3 companies. Verify all 3 appear in results.

### Files

| Action | File |
|--------|------|
| Modify | `dotnet/Stocks.Persistence/Services/MoatScoringService.cs` |
| Modify | `dotnet/Stocks.EDGARScraper/Program.cs` |
| Create | `dotnet/Stocks.EDGARScraper.Tests/Scoring/BatchMoatScoringServiceTests.cs` |

### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln --filter "FullyQualifiedName~BatchMoatScoringServiceTests"
```

---

## Checkpoint 6: Backend API Endpoints

**Goal:** Create the Moat Score API endpoints: on-demand single-company scoring and pre-computed list report. Register them in the WebApi startup.

### Build

**Create `dotnet/Stocks.WebApi/Endpoints/MoatScoringEndpoints.cs`:**

Single endpoint: `GET /api/companies/{cik}/moat-scoring`

Follow the pattern of `ScoringEndpoints.cs`:
1. Resolve company by CIK via `dbm.GetCompanyByCik(cik, ct)`.
2. Call `moatScoringService.ComputeScore(company.CompanyId, ct)`.
3. Return JSON with:
   - `rawDataByYear` — same structure as Value Score
   - `metrics` — all `MoatDerivedMetrics` fields (camelCase)
   - `scorecard` — array of `{ checkNumber, name, computedValue, threshold, result }`
   - `trendData` — array of `{ year, grossMarginPct, operatingMarginPct, roeCfPct, roeOePct, revenue }`
   - `overallScore`, `computableChecks`, `yearsOfData`
   - `pricePerShare`, `priceDate`, `sharesOutstanding`

Register `MoatScoringService` as a scoped/singleton service in the WebApi DI container, or construct it within the endpoint handler (same approach as `ScoringEndpoints.cs`).

**Create `dotnet/Stocks.WebApi/Endpoints/MoatReportEndpoints.cs`:**

Single endpoint: `GET /api/reports/moat-scores`

Follow the pattern of `ReportEndpoints.cs`:
1. Parse query params: `page`, `pageSize`, `sortBy`, `sortDir`, `minScore`, `maxScore`, `exchange`.
2. Map `sortBy` string to `MoatScoresSortBy` enum (supported columns: `averageGrossMargin`, `averageOperatingMargin`, `averageRoeCF`, `averageRoeOE`, `estimatedReturnOE`, `revenueCagr`, `capexRatio`, `interestCoverage`, `debtToEquityRatio`; default: `OverallScore`).
3. Call `dbm.GetCompanyMoatScores(pagination, sortBy, sortDir, filter, ct)`.
4. Return `PagedResults<CompanyMoatScoreSummary>`.

**Modify WebApi startup (endpoint registration):**

Find where existing endpoints are mapped (likely `Program.cs` or a startup extension method in the WebApi project) and add:
```csharp
app.MapMoatScoringEndpoints();
app.MapMoatReportEndpoints();
```

### Test

No dedicated unit tests for this checkpoint. The API endpoints are thin delegation layers that will be tested end-to-end when the frontend is connected. The full test suite run in the Verify step confirms no compilation regressions.

### Files

| Action | File |
|--------|------|
| Create | `dotnet/Stocks.WebApi/Endpoints/MoatScoringEndpoints.cs` |
| Create | `dotnet/Stocks.WebApi/Endpoints/MoatReportEndpoints.cs` |
| Modify | WebApi startup/Program.cs (endpoint registration) |

### Verify

```bash
dotnet build dotnet/EDGARScraper.sln
dotnet test dotnet/EDGARScraper.sln
```

Full test suite must pass (no regressions).

---

## Checkpoint 7: Frontend — API Service and Moat Detail Page

**Goal:** Add Moat TypeScript interfaces and API methods, extract a reusable sparkline helper, and create the Moat Score detail page component with scorecard, metrics, and 6 trend charts.

### Build

**Modify `frontend/stocks-frontend/src/app/core/services/api.service.ts`:**

Add interfaces:

```typescript
export interface MoatDerivedMetricsResponse {
  averageGrossMargin: number | null;
  averageOperatingMargin: number | null;
  averageRoeCF: number | null;
  averageRoeOE: number | null;
  revenueCagr: number | null;
  capexRatio: number | null;
  interestCoverage: number | null;
  debtToEquityRatio: number | null;
  estimatedReturnOE: number | null;
  currentDividendsPaid: number | null;
  marketCap: number | null;
  pricePerShare: number | null;
  positiveOeYears: number;
  totalOeYears: number;
  capitalReturnYears: number;
  totalCapitalReturnYears: number;
}

export interface MoatYearMetrics {
  year: number;
  grossMarginPct: number | null;
  operatingMarginPct: number | null;
  roeCfPct: number | null;
  roeOePct: number | null;
  revenue: number | null;
}

export interface MoatScoringResponse {
  rawDataByYear: Record<string, Record<string, number>>;
  metrics: MoatDerivedMetricsResponse;
  scorecard: ScoringCheckResponse[];   // reuse existing interface
  trendData: MoatYearMetrics[];
  overallScore: number;
  computableChecks: number;
  yearsOfData: number;
  pricePerShare: number | null;
  priceDate: string | null;
  sharesOutstanding: number | null;
}

export interface CompanyMoatScoreSummary {
  companyId: number;
  cik: string;
  companyName: string | null;
  ticker: string | null;
  exchange: string | null;
  overallScore: number;
  computableChecks: number;
  yearsOfData: number;
  averageGrossMargin: number | null;
  averageOperatingMargin: number | null;
  averageRoeCF: number | null;
  averageRoeOE: number | null;
  estimatedReturnOE: number | null;
  revenueCagr: number | null;
  capexRatio: number | null;
  interestCoverage: number | null;
  debtToEquityRatio: number | null;
  pricePerShare: number | null;
  priceDate: string | null;
  sharesOutstanding: number | null;
  computedAt: string;
}
```

Add API methods:

```typescript
getMoatScoring(cik: string): Observable<MoatScoringResponse> {
  return this.http.get<MoatScoringResponse>(`${this.apiUrl}/companies/${cik}/moat-scoring`);
}

getMoatScoresReport(params: ScoresReportParams): Observable<PaginatedResponse<CompanyMoatScoreSummary>> {
  // same pattern as getScoresReport but hitting /api/reports/moat-scores
}
```

**Create `frontend/stocks-frontend/src/app/shared/sparkline.utils.ts`:**

Extract the sparkline SVG computation logic from `ScoringComponent` (lines 372-438) into a reusable utility function:

```typescript
export interface SparklinePoint { x: number; y: number; label: string; value: number; }
export interface SparklineTick { y: number; label: string; }
export interface SparklineData {
  points: SparklinePoint[];
  yTicks: SparklineTick[];
  viewBox: string;
  polylinePoints: string;
}

export function computeSparkline(
  data: { label: string; value: number }[],
  options?: { yAxisFormat?: 'percent' | 'currency'; viewBoxWidth?: number; viewBoxHeight?: number }
): SparklineData | null
```

This function takes an array of `{ label, value }` pairs (chronologically ordered, already filtered for non-null) and returns the SVG data structure. The `ScoringComponent` can then be refactored to use this utility (optional — not required for this checkpoint, but the Moat component will use it).

**Create `frontend/stocks-frontend/src/app/features/moat-scoring/moat-scoring.component.ts`:**

Standalone Angular component modeled on `ScoringComponent`. Key sections:

1. **Component metadata:** `selector: 'app-moat-scoring'`, standalone, inline template and styles.
2. **Data fetching:** On `ngOnInit`, make 3 parallel API calls:
   - `api.getCompany(cik)` — company header info
   - `api.getMoatScoring(cik)` — moat scoring result with trend data
   - `api.getArRevenue(cik)` — AR/Revenue data (reuse existing endpoint)
3. **Signals:** `company`, `scoring`, `loading`, `error`, `arRevenueRows`.
4. **Computed signals for sparklines:** 6 sparkline signals using `computeSparkline`:
   - AR/Revenue % — from `arRevenueRows` (same as Value Score)
   - Gross Margin % — from `scoring.trendData` mapping `year → grossMarginPct`
   - Operating Margin % — from `scoring.trendData` mapping `year → operatingMarginPct`
   - ROE (CF) % — from `scoring.trendData` mapping `year → roeCfPct`
   - ROE (OE) % — from `scoring.trendData` mapping `year → roeOePct`
   - Revenue — from `scoring.trendData` mapping `year → revenue` (currency format)
5. **Template sections:**
   - Breadcrumb: Home / CIK / Moat Score
   - Company header (reuse same pattern as Value Score)
   - Score badge (colored green/yellow/red, showing score/computableChecks)
   - Score caption (years of data, price, shares outstanding)
   - Scorecard table (13 checks: #, Name, Value, Threshold, Result)
   - Derived Metrics table (Moat-specific rows: Avg Gross Margin, Avg Operating Margin, Avg ROE CF/OE, Revenue CAGR, CapEx Ratio, Interest Coverage, D/E Ratio, Est. Return OE, Market Cap, Dividends Paid)
   - 6 Trend chart sections — each with a data table + SVG sparkline side by side, using `computeSparkline`. Show "Not enough data" if < 2 data points.
   - Raw Data table (all concepts by year)

### Test

No automated Angular tests. Verify by:
- `ng build` compiles successfully.
- Visual inspection after backend endpoints are running.

### Files

| Action | File |
|--------|------|
| Modify | `frontend/stocks-frontend/src/app/core/services/api.service.ts` |
| Create | `frontend/stocks-frontend/src/app/shared/sparkline.utils.ts` |
| Create | `frontend/stocks-frontend/src/app/features/moat-scoring/moat-scoring.component.ts` |

### Verify

```bash
cd frontend/stocks-frontend && npx ng build
```

---

## Checkpoint 8: Frontend — Moat List Page and Navigation

**Goal:** Create the Moat Scores list/report page, add routes, and add sidebar navigation entry.

### Build

**Create `frontend/stocks-frontend/src/app/features/moat-scores-report/moat-scores-report.component.ts`:**

Standalone Angular component modeled on `ScoresReportComponent`. Key differences from Value Score list:

1. **Columns:** Score, Company (link to `/company/:cik/moat-scoring`), Ticker, Exchange, Price, Gross Margin, Operating Margin, Avg ROE CF, Avg ROE OE, Est. Return (OE), Revenue CAGR.
2. **Sortable columns:** Maps to `MoatScoresSortBy` enum values: `averageGrossMargin`, `averageOperatingMargin`, `averageRoeCF`, `averageRoeOE`, `estimatedReturnOE`, `revenueCagr`, `capexRatio`, `interestCoverage`, `debtToEquityRatio`.
3. **Filtering:** Same as Value Score — Min Score dropdown, Exchange dropdown, Page Size dropdown.
4. **Data fetching:** `api.getMoatScoresReport(params)`.
5. **Color coding:** Same `scoreBadgeClass` logic — green (>= 10), yellow (>= 7), red (< 7). Row highlight for perfect (13/13) and near-perfect (12/13).
6. **Formatting:** `fmtPct` for margins, ROE, CAGR, est. return. `fmtPrice` for price.
7. **Pagination:** Same Previous/Next pattern.

**Modify `frontend/stocks-frontend/src/app/app.routes.ts`:**

Add two new routes:

```typescript
{ path: 'moat-scores', title: 'Stocks - Moat Scores', loadComponent: () => import('./features/moat-scores-report/moat-scores-report.component').then(m => m.MoatScoresReportComponent) },
{ path: 'company/:cik/moat-scoring', title: 'Stocks - Moat Score', loadComponent: () => import('./features/moat-scoring/moat-scoring.component').then(m => m.MoatScoringComponent) },
```

**Modify `frontend/stocks-frontend/src/app/core/layout/sidebar/sidebar.component.ts`:**

Add a "Moat Scores" entry below "Value Scores":

```html
<li>
  <a routerLink="/moat-scores" routerLinkActive="active"
     title="Rewards competitive moat: high margins, consistent returns, capital-light operations, and long track records">
    Moat Scores
  </a>
</li>
```

### Test

No automated Angular tests. Verify by:
- `ng build` compiles successfully.
- Visual inspection: sidebar shows "Moat Scores" link, clicking navigates to list page, clicking a company navigates to detail page.

### Files

| Action | File |
|--------|------|
| Create | `frontend/stocks-frontend/src/app/features/moat-scores-report/moat-scores-report.component.ts` |
| Modify | `frontend/stocks-frontend/src/app/app.routes.ts` |
| Modify | `frontend/stocks-frontend/src/app/core/layout/sidebar/sidebar.component.ts` |

### Verify

```bash
cd frontend/stocks-frontend && npx ng build
```

---

<!-- Self-review: converged after 3 passes -->
