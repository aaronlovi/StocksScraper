# Research: Company Scoring System (13-Point Value Score)

## Table of Contents

- [1. Formulas](#1-formulas)
  - [1.1. Formulas - Balance Sheet Derived Metrics](#11-formulas---balance-sheet-derived-metrics)
  - [1.2. Formulas - Cash Flow Derived Metrics](#12-formulas---cash-flow-derived-metrics)
  - [1.3. Formulas - Valuation Derived Metrics](#13-formulas---valuation-derived-metrics)
  - [1.4. Formulas - The 13 Checks](#14-formulas---the-13-checks)
- [2. Concept Mapping](#2-concept-mapping)
  - [2.1. Concept Mapping - Balance Sheet Fields](#21-concept-mapping---balance-sheet-fields)
  - [2.2. Concept Mapping - Cash Flow Fields](#22-concept-mapping---cash-flow-fields)
  - [2.3. Concept Mapping - Income Statement Fields](#23-concept-mapping---income-statement-fields)
  - [2.4. Concept Mapping - Market Data Fields](#24-concept-mapping---market-data-fields)
- [3. Data Availability](#3-data-availability)
  - [3.1. Data Availability - Individual Concept Coverage](#31-data-availability---individual-concept-coverage)
  - [3.2. Data Availability - Union Coverage for Grouped Fields](#32-data-availability---union-coverage-for-grouped-fields)
- [4. Multi-Year Depth Analysis](#4-multi-year-depth-analysis)
  - [4.1. Multi-Year Depth Analysis - Overall Depth](#41-multi-year-depth-analysis---overall-depth)
  - [4.2. Multi-Year Depth Analysis - Per-Concept Depth](#42-multi-year-depth-analysis---per-concept-depth)
- [5. Feasibility Classification Per Check](#5-feasibility-classification-per-check)
- [6. Recommendations](#6-recommendations)
  - [6.1. Recommendations - Concept Selection Strategy](#61-recommendations---concept-selection-strategy)
  - [6.2. Recommendations - Implementation Approach](#62-recommendations---implementation-approach)
  - [6.3. Recommendations - Handling Missing Data](#63-recommendations---handling-missing-data)
- [Metadata](#metadata)

## 1. Formulas

Extracted from `tsx-aggregator/src/tsx-aggregator.models/AggregatorData/CompanyReport.cs`, `CompanyFullDetailReport.cs`, and `CashFlowItem.cs`.

### 1.1. Formulas - Balance Sheet Derived Metrics

**Book Value:**
```
BookValue = TotalShareholdersEquity - (Goodwill + Intangibles)
```

**Adjusted Retained Earnings:**
```
AdjustedRetainedEarnings = CurrentRetainedEarnings + TotalDividendsPaid - TotalCommonStockIssuance - TotalPreferredStockIssuance
```
Where the totals are summed across all annual cash flow reports processed.

**Working Capital (fallback calculation):**
```
WorkingCapital = CurrentAssets - CurrentLiabilities
ChangeInWorkingCapital = CurrentWorkingCapital - PreviousWorkingCapital
```

### 1.2. Formulas - Cash Flow Derived Metrics

**Gross Cash Flow:**
```
GrossCashFlow = ChangesInCash
  (fallback: EndCashPosition - BeginningCashPosition)
```

**Net Cash Flow:**
```
NetCashFlow = GrossCashFlow - (NetIssuanceOfDebt + NetIssuanceOfStock + NetIssuanceOfPreferredStock)
```

**Owner Earnings:**
```
OwnerEarnings = NetIncomeFromContinuingOperations
  + Depreciation
  + Depletion
  + Amortization
  + DeferredTax
  + OtherNonCashItems
  - CapEx
  + ChangeInWorkingCapital

Where:
  CapEx = NetPPEPurchaseAndSale + Depreciation
```

Note: Depreciation appears twice and cancels out. Expanding the formula:
```
OwnerEarnings = NetIncome + Depletion + Amortization + DeferredTax
  + OtherNonCashItems - NetPPEPurchaseAndSale + ChangeInWorkingCapital
```
This is the effective formula for implementation. **Depreciation does not need to be queried** — it cancels out. The Depreciation entries in sections 2-3 below are retained for traceability to the tsx-aggregator source but are not required concepts for our implementation.

**Averages (over all available annual reports):**
```
AverageNetCashFlow = SUM(annual NetCashFlow) / NumAnnualReports
AverageOwnerEarnings = SUM(annual OwnerEarnings) / NumAnnualReports
```

### 1.3. Formulas - Valuation Derived Metrics

**Market Cap:**
```
MarketCap = NumShares × PricePerShare
```

**Estimated Next Year Book Value:**
```
EstBookValue_CF = BookValue + AverageNetCashFlow
EstBookValue_OE = BookValue + AverageOwnerEarnings
```

**Estimated Return:**
```
EstReturn_CF = 100 × (EstBookValue_CF - DividendsPaid - BookValue) / MarketCap
EstReturn_OE = 100 × (EstBookValue_OE - DividendsPaid - BookValue) / MarketCap
```

Since `EstBookValue - BookValue = Average(NetCashFlow or OwnerEarnings)`, these simplify to:
```
EstReturn_CF = 100 × (AverageNetCashFlow - DividendsPaid) / MarketCap
EstReturn_OE = 100 × (AverageOwnerEarnings - DividendsPaid) / MarketCap
```

### 1.4. Formulas - The 13 Checks

| # | Check | Formula | Pass Condition |
|---|-------|---------|---------------|
| 1 | Debt-to-Equity | LongTermDebt / TotalShareholdersEquity | < 0.5 |
| 2 | Book Value | Equity - (Goodwill + Intangibles) | > $150M |
| 3 | Price-to-Book | MarketCap / BookValue | ≤ 3.0 |
| 4 | Avg Net Cash Flow Positive | AverageNetCashFlow | > 0 |
| 5 | Avg Owner Earnings Positive | AverageOwnerEarnings | > 0 |
| 6 | Est. Return (CF) Big Enough | EstReturn_CF | > 5% |
| 7 | Est. Return (OE) Big Enough | EstReturn_OE | > 5% |
| 8 | Est. Return (CF) Not Too Big | EstReturn_CF | < 40% |
| 9 | Est. Return (OE) Not Too Big | EstReturn_OE | < 40% |
| 10 | Debt-to-Book | LongTermDebt / BookValue | < 1.0 |
| 11 | Retained Earnings Positive | AdjustedRetainedEarnings | > 0 |
| 12 | History Long Enough | NumAnnualCashFlowReports | ≥ 4 |
| 13 | Retained Earnings Increased | AdjustedRetainedEarnings > OldestRetainedEarnings | true |

## 2. Concept Mapping

### 2.1. Concept Mapping - Balance Sheet Fields

| tsx-aggregator Field | US-GAAP Concept(s) | Period Type |
|---|---|---|
| StockholdersEquity | `StockholdersEquity` (primary), `StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest` (fallback) | instant |
| Goodwill | `Goodwill` | instant |
| OtherIntangibleAssets | `IntangibleAssetsNetExcludingGoodwill` | instant |
| LongTermDebt | `LongTermDebtAndCapitalLeaseObligations` (primary), `LongTermDebt` (fallback), `LongTermDebtNoncurrent` (fallback) | instant |
| RetainedEarnings | `RetainedEarningsAccumulatedDeficit` | instant |
| CurrentAssets | `AssetsCurrent` | instant |
| CurrentLiabilities | `LiabilitiesCurrent` | instant |

### 2.2. Concept Mapping - Cash Flow Fields

| tsx-aggregator Field | US-GAAP Concept(s) | Period Type |
|---|---|---|
| ChangesInCash (GrossCashFlow) | `CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect` (primary), `CashAndCashEquivalentsPeriodIncreaseDecrease` (fallback) | duration |
| BeginningCashPosition (fallback only) | `CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalents`, `CashAndCashEquivalentsAtCarryingValue` | instant |
| EndCashPosition (fallback only) | Same concepts as above | instant |
| NetIssuancePaymentsOfDebt | Compute: `ProceedsFromIssuanceOfLongTermDebt` - `RepaymentsOfLongTermDebt` | duration |
| CashDividendsPaid | `PaymentsOfDividends` (primary), `PaymentsOfDividendsCommonStock` (fallback) | duration |
| NetCommonStockIssuance | Compute: `ProceedsFromIssuanceOfCommonStock` - `PaymentsForRepurchaseOfCommonStock` | duration |
| NetPreferredStockIssuance | Compute: `ProceedsFromIssuanceOfPreferredStockAndPreferenceStock` - `PaymentsForRepurchaseOfPreferredStockAndPreferenceStock` | duration |
| ChangeInWorkingCapital | `IncreaseDecreaseInOperatingCapital` (primary), `IncreaseDecreaseInOtherOperatingCapitalNet` (fallback), or compute from balance sheet diffs | duration |
| Depreciation (not needed — see note in 1.2) | `Depreciation` (primary), `DepreciationAndAmortization` (fallback), `DepreciationDepletionAndAmortization` (broader fallback) | duration |
| Depletion | `Depletion` | duration |
| Amortization | `AmortizationOfIntangibleAssets` | duration |
| DeferredTax | `DeferredIncomeTaxExpenseBenefit` (primary), `DeferredIncomeTaxesAndTaxCredits` (fallback) | duration |
| OtherNonCashItems | `OtherNoncashIncomeExpense` | duration |
| NetPPEPurchaseAndSale (CapEx input) | `PaymentsToAcquirePropertyPlantAndEquipment` | duration |

**Note on cash position fallback:** Using BeginningCashPosition/EndCashPosition to compute GrossCashFlow requires identifying prior-year vs. current-year instant values for the same concept. This is complex. Given 87.3% union coverage on the direct cash change concepts, this fallback is low priority — consider skipping it initially.

**Sign convention note:** In US-GAAP XBRL, `PaymentsToAcquire*` and `PaymentsFor*` concepts are reported as **positive** values representing cash outflows. The tsx-aggregator treats these as positive outflows too. When computing Net Cash Flow, subtract financing outflows (debt repayment, stock repurchase) from Gross Cash Flow. For CapEx/Owner Earnings, `PaymentsToAcquirePropertyPlantAndEquipment` maps directly to `NetPPEPurchaseAndSale` without sign inversion. Verify sign conventions against sample companies (e.g., Apple CIK 320193) during implementation.

### 2.3. Concept Mapping - Income Statement Fields

| tsx-aggregator Field | US-GAAP Concept(s) | Period Type |
|---|---|---|
| NetIncomeFromContinuingOperations | `NetIncomeLoss` (primary), `IncomeLossFromContinuingOperations` (fallback), `ProfitLoss` (fallback) | duration |

### 2.4. Concept Mapping - Market Data Fields

| tsx-aggregator Field | Source | Notes |
|---|---|---|
| PricePerShare | `prices` table (daily OHLC) | Use most recent close price for the company's ticker |
| CurNumShares | `CommonStockSharesOutstanding` from `data_points` table (instant, 77.6% coverage) | Use most recent 10-K value |

## 3. Data Availability

Base: ~13,490 companies have 10-K filings with data points in the database.

### 3.1. Data Availability - Individual Concept Coverage

| US-GAAP Concept | Companies | Coverage % |
|---|---|---|
| **Balance Sheet** | | |
| `StockholdersEquity` | 12,135 | 89.9% |
| `RetainedEarningsAccumulatedDeficit` | 12,006 | 89.0% |
| `AssetsCurrent` | 10,526 | 78.0% |
| `LiabilitiesCurrent` | 10,537 | 78.1% |
| `CommonStockSharesOutstanding` | 10,462 | 77.6% |
| `Goodwill` | 6,046 | 44.8% |
| `LongTermDebt` | 5,664 | 42.0% |
| `IntangibleAssetsNetExcludingGoodwill` | 5,225 | 38.7% |
| `StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest` | 4,872 | 36.1% |
| `LongTermDebtNoncurrent` | 4,340 | 32.2% |
| `LongTermDebtAndCapitalLeaseObligations` | 1,425 | 10.6% |
| **Cash Flow** | | |
| `CashAndCashEquivalentsAtCarryingValue` | 11,881 | 88.1% |
| `CashAndCashEquivalentsPeriodIncreaseDecrease` | 9,610 | 71.2% |
| `PaymentsToAcquirePropertyPlantAndEquipment` | 8,589 | 63.7% |
| `ProceedsFromIssuanceOfCommonStock` | 8,293 | 61.5% |
| `Depreciation` | 7,643 | 56.6% |
| `DeferredIncomeTaxExpenseBenefit` | 7,416 | 55.0% |
| `DepreciationDepletionAndAmortization` | 6,489 | 48.1% |
| `CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect` | 6,474 | 48.0% |
| `AmortizationOfIntangibleAssets` | 6,288 | 46.6% |
| `PaymentsForRepurchaseOfCommonStock` | 5,474 | 40.6% |
| `DepreciationAndAmortization` | 4,679 | 34.7% |
| `RepaymentsOfLongTermDebt` | 3,952 | 29.3% |
| `ProceedsFromIssuanceOfLongTermDebt` | 3,633 | 26.9% |
| `OtherNoncashIncomeExpense` | 2,856 | 21.2% |
| `PaymentsOfDividends` | 2,278 | 16.9% |
| `PaymentsOfDividendsCommonStock` | 2,276 | 16.9% |
| `IncreaseDecreaseInOtherOperatingCapitalNet` | 1,586 | 11.8% |
| `ProceedsFromIssuanceOfPreferredStockAndPreferenceStock` | 1,526 | 11.3% |
| `IncreaseDecreaseInOperatingCapital` | 452 | 3.4% |
| `Depletion` | 106 | 0.8% |
| **Income Statement** | | |
| `NetIncomeLoss` | 12,896 | 95.6% |
| `ProfitLoss` | 7,417 | 55.0% |
| `IncomeLossFromContinuingOperations` | 3,177 | 23.6% |

### 3.2. Data Availability - Union Coverage for Grouped Fields

For fields where tsx-aggregator uses fallback logic, the union (companies with at least one variant) is more relevant:

| Field Group | Concepts in Union | Companies | Coverage % |
|---|---|---|---|
| **Net Income** | `NetIncomeLoss`, `IncomeLossFromContinuingOperations`, `ProfitLoss`, `IncomeLossFromContinuingOperationsIncludingPortionAttributableToNoncontrollingInterest` | 13,241 | 98.2% |
| **Cash Change (GrossCashFlow)** | `CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect`, `CashAndCashEquivalentsPeriodIncreaseDecrease` | 11,776 | 87.3% |
| **Depreciation** | `Depreciation`, `DepreciationAndAmortization`, `DepreciationDepletionAndAmortization` | 10,577 | 78.4% |
| **Stock Issuance/Repurchase** | `ProceedsFromIssuanceOfCommonStock`, `PaymentsForRepurchaseOfCommonStock` | 10,413 | 77.2% |
| **Long-Term Debt** | `LongTermDebt`, `LongTermDebtNoncurrent`, `LongTermDebtAndCapitalLeaseObligations` | 6,885 | 51.0% |
| **Dividends Paid** | `PaymentsOfDividends`, `PaymentsOfDividendsCommonStock`, `Dividends` | 4,351 | 32.3% |

**Note on dividends:** Only ~32% of companies report dividends because most companies simply don't pay dividends. A missing dividends concept likely means $0 dividends paid — this is data absence, not data quality issue. Treat missing dividends as $0.

**Note on long-term debt:** Only ~51% have any long-term debt concept. Many companies (especially smaller ones, pre-revenue biotech, tech startups) genuinely have zero or negligible long-term debt. For the debt-to-equity check, a missing debt value can safely default to $0 (which trivially passes the check).

## 4. Multi-Year Depth Analysis

### 4.1. Multi-Year Depth Analysis - Overall Depth

Out of 13,490 companies with 10-K data points:

| Threshold | Companies | Percentage |
|---|---|---|
| ≥ 4 distinct report years | 8,514 | 63.1% |
| ≥ 5 distinct report years | 7,090 | 52.6% |

### 4.2. Multi-Year Depth Analysis - Per-Concept Depth

| Concept | ≥ 4 Years | ≥ 5 Years | Total w/ Concept |
|---|---|---|---|
| `NetIncomeLoss` | 7,589 | 6,291 | 12,896 |
| `RetainedEarningsAccumulatedDeficit` | 7,297 | 6,104 | 12,006 |
| `StockholdersEquity` | 7,050 | 5,924 | 12,135 |
| `CommonStockSharesOutstanding` | 6,145 | 5,104 | 10,462 |
| `PaymentsToAcquirePropertyPlantAndEquipment` | 5,129 | 4,245 | 8,589 |
| `Depreciation` | 4,386 | 3,581 | 7,643 |
| `DepreciationDepletionAndAmortization` | 3,862 | 3,221 | 6,489 |
| `LongTermDebt` | 2,763 | 2,199 | 5,664 |
| `LongTermDebtNoncurrent` | 2,412 | 1,944 | 4,340 |

**Key finding:** ~7,000-7,600 companies have the core concepts (Net Income, Retained Earnings, Equity) across 4+ years. This is roughly the "scoreable universe" — companies where we can reliably compute most of the 13 checks.

## 5. Feasibility Classification Per Check

| # | Check | Classification | Rationale |
|---|---|---|---|
| 1 | Debt-to-Equity | **Reliable** | StockholdersEquity at 89.9%. LongTermDebt union at 51%, but missing debt → $0 → trivial pass. |
| 2 | Book Value > $150M | **Reliable** | StockholdersEquity 89.9%, Goodwill 44.8%, Intangibles 38.7%. Missing goodwill/intangibles → $0 → BookValue = Equity (conservative). |
| 3 | Price-to-Book ≤ 3.0 | **Reliable** | Requires BookValue (reliable) + market cap (price from prices table + shares outstanding at 77.6%). |
| 4 | Avg Net Cash Flow Positive | **Partial** | GrossCashFlow union 87.3%, but NetIssuanceOfDebt components at 27-29% individually. Feasible with $0 defaults for missing financing items. |
| 5 | Avg Owner Earnings Positive | **Partial** | Net Income 98.2%, CapEx 63.7% (Depreciation not needed — cancels out). ChangeInWorkingCapital computable from AssetsCurrent/LiabilitiesCurrent diffs (78%). Minor components (Depletion, OtherNonCash) default to $0. |
| 6-9 | Est. Return bounds (CF & OE) | **Partial** | Derived from checks 4-5 + BookValue + MarketCap. Same data availability caveats apply. |
| 10 | Debt-to-Book < 1.0 | **Reliable** | Same inputs as checks 1 and 2. Missing debt defaults to $0. |
| 11 | Retained Earnings Positive | **Reliable** | RetainedEarningsAccumulatedDeficit at 89.0%. AdjustedRetainedEarnings adds dividends and stock issuance adjustments — dividends default to $0, stock issuance union at 77.2%. |
| 12 | History ≥ 4 Years | **Reliable** | 8,514 companies (63.1%) have ≥ 4 years of 10-K data. This is a data volume check, not a concept availability issue. |
| 13 | Retained Earnings Increased | **Reliable** | Requires RetainedEarnings in both oldest and current report. 7,297 companies have RetainedEarnings in ≥ 4 years. |

**Summary: 8 checks Reliable, 5 checks Partial.** No check is classified as Risky — all have viable paths.

## 6. Recommendations

### 6.1. Recommendations - Concept Selection Strategy

Use a prioritized fallback chain for each field. When the primary concept is absent, try alternatives in order. If all are absent, use a sensible default ($0 for debt, dividends, stock issuance; mark the field as unavailable for equity, net income, and cash flow).

**Recommended concept priority chains:**

1. **Shareholders Equity:** `StockholdersEquity` → `StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest`
2. **Long-Term Debt:** `LongTermDebtAndCapitalLeaseObligations` → `LongTermDebt` → `LongTermDebtNoncurrent` → default $0
3. **Retained Earnings:** `RetainedEarningsAccumulatedDeficit` (single concept, 89% coverage)
4. **Goodwill:** `Goodwill` → default $0
5. **Intangibles:** `IntangibleAssetsNetExcludingGoodwill` → default $0
6. **Net Income:** `NetIncomeLoss` → `IncomeLossFromContinuingOperations` → `ProfitLoss`
7. **Cash Change:** `CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect` → `CashAndCashEquivalentsPeriodIncreaseDecrease`
8. **CapEx (NetPPEPurchaseAndSale):** `PaymentsToAcquirePropertyPlantAndEquipment` → default $0
   - Note: Depreciation is not needed — it cancels out in the Owner Earnings formula (see section 1.2)
9. **Dividends:** `PaymentsOfDividends` → `PaymentsOfDividendsCommonStock` → `Dividends` → default $0
10. **Stock Issuance (net):** Compute from `ProceedsFromIssuanceOfCommonStock` - `PaymentsForRepurchaseOfCommonStock`, each defaulting to $0
11. **Deferred Tax:** `DeferredIncomeTaxExpenseBenefit` → `DeferredIncomeTaxesAndTaxCredits` → default $0
12. **Depletion:** `Depletion` → default $0 (only 106 companies, 0.8%)
13. **Amortization:** `AmortizationOfIntangibleAssets` → default $0 (46.6% coverage)
14. **Other Non-Cash Items:** `OtherNoncashIncomeExpense` → default $0 (21.2% coverage)
15. **Shares Outstanding:** `CommonStockSharesOutstanding` (from data_points)
16. **Change in Working Capital:** `IncreaseDecreaseInOperatingCapital` → `IncreaseDecreaseInOtherOperatingCapitalNet` → compute from YoY change in (`AssetsCurrent` - `LiabilitiesCurrent`) → default $0

### 6.2. Recommendations - Implementation Approach

1. **Compute scores server-side** in a new service or endpoint, not in the frontend. The scoring requires multi-year data aggregation across multiple concepts per company.

2. **Score on demand** per company (not batch). When a user views a company's page, fetch the last 5 years of 10-K data points for the relevant concepts, compute derived metrics, and return the 13-check scorecard.

3. **Show partial scores.** If a company has only 3 years of data, compute what's available and show "N/A" or "Insufficient data" for checks that require more history (checks 4-9, 12-13). Display the score as "X / Y computable checks passed" rather than hiding the entire scorecard.

4. **Cache scores** optionally — since 10-K data doesn't change after import, scores are stable until new filings are imported or prices change.

### 6.3. Recommendations - Handling Missing Data

| Situation | Recommendation |
|---|---|
| Missing debt concepts | Default to $0 — trivially passes debt ratio checks |
| Missing goodwill/intangibles | Default to $0 — BookValue = Equity (conservative, may overstate) |
| Missing dividends | Default to $0 — most companies don't pay dividends |
| Missing stock issuance/repurchase | Default to $0 for each side |
| Missing depletion | Default to $0 — only 106 companies report it (mining/oil industry) |
| Missing other non-cash items | Default to $0 |
| Missing equity | Cannot compute score — mark as "Insufficient data" |
| Missing net income | Cannot compute Owner Earnings — mark OE-based checks as N/A |
| Missing cash change | Cannot compute Net Cash Flow — mark CF-based checks as N/A |
| < 4 years of data | Check 12 fails automatically; checks 4-9 use available years; check 13 uses what's available |

## Metadata

### Status
success

### Dependencies
- `tsx-aggregator/src/tsx-aggregator.models/AggregatorData/CompanyFullDetailReport.cs` — scoring checks and valuation formulas
- `tsx-aggregator/src/tsx-aggregator.models/AggregatorData/CompanyReport.cs` — raw field processing and derived metric formulas
- `tsx-aggregator/src/tsx-aggregator.models/AggregatorData/CashFlowItem.cs` — NetCashFlow and OwnerEarnings formulas
- `prices` table — stock prices for market cap calculation
- `data_points` + `taxonomy_concepts` tables — XBRL financial data
- `submissions` table — filing metadata (filing_type, report_date)

### Open Questions
- Should we use the most recent filing's shares outstanding, or a separate shares data source? `CommonStockSharesOutstanding` from data_points is at 77.6% coverage — some companies may report shares differently.
- The tsx-aggregator's `NetPPEPurchaseAndSale` field name suggests net of purchases and sales, but the US-GAAP `PaymentsToAcquirePropertyPlantAndEquipment` is only the purchase side. Should we also subtract `ProceedsFromSaleOfPropertyPlantAndEquipment` for a true net CapEx figure?
- Sign conventions should be verified against sample data (e.g., Apple CIK 320193) during implementation. See the sign convention note in section 2.2.

### Assumptions
- Companies missing a debt concept genuinely have no (or negligible) long-term debt — defaulting to $0 is appropriate.
- Companies missing dividend concepts pay no dividends — defaulting to $0 is appropriate.
- 10-K filings (filing_type = 1) are the appropriate data source for annual financial data.
- The `prices` table has sufficiently recent price data for market cap calculations.
- The scoring system should be adapted (not replicated verbatim) from tsx-aggregator, since that system scraped Yahoo Finance field names for TSX companies, while our database has raw US-GAAP XBRL concepts from SEC EDGAR.
