# Taxonomy Mapping Requirements

## Summary

Define a mapping layer that converts EDGAR taxonomy concepts into the normalized `CompanyReport` and `CashFlowItem` fields used for comparison data. The mapping should use the taxonomy metadata stored in `taxonomy_concepts` (name, label, documentation) to propose candidate concepts, apply ranking/priority rules, and populate raw report maps for annual and non-annual statements. The initial focus is on US GAAP 2025, with a plan to decide whether additional taxonomy years should be imported for broader coverage.

## Requirements Table

| ID | Requirement | Description | Status | Notes |
| --- | --- | --- | --- | --- |
| 1 | Mapping config | Create a structured mapping definition per `CompanyReport`/`CashFlowItem` field. | Proposed | Include keyword rules and priority lists. |
| 2 | Candidate discovery | Use `taxonomy_concepts` name/label/documentation to find candidates per field. | Proposed | Score based on keyword matches and exclude negatives. |
| 3 | Period handling | Respect period type (instant vs duration) when selecting data points. | Proposed | Annual vs non-annual alignment required. |
| 4 | Field selection | Choose the highest-ranked candidate available per report date. | Proposed | Prefer exact name matches over fuzzy label matches. |
| 5 | Raw report maps | Populate `RawReportDataMap` per statement and report date. | Proposed | Use normalized keys for downstream processing. |
| 6 | Company report build | Build `CompanyReport` from EDGAR data points plus mapping. | Proposed | Integrate with existing statement processing. |
| 7 | Diagnostics | Emit warnings for missing or ambiguous mappings. | Proposed | Collect in warnings list for review. |
| 8 | Tests | Add xUnit tests for mapping selection and report assembly. | Proposed | No LINQ in production paths. |
| 9 | Multi-year taxonomy decision | Decide whether to import taxonomies for years other than 2025. | Proposed | Capture decision in requirements and ADR. |

## Kanban Task List (<= 2h each)

- [ ] Inventory `CompanyReport` + `CashFlowItem` fields and define mapping targets.
- [ ] Draft mapping rules (keyword lists, exclusions, priority order).
- [ ] Implement candidate scoring using `taxonomy_concepts`.
- [ ] Implement report builder that selects data points per report date.
- [ ] Wire report builder into a CLI or service entry point.
- [ ] Add tests for mapping selection and report generation.
- [ ] Decide on multi-year taxonomy import strategy; document in ADR.

## High-Level Design

1) Load EDGAR data points for a company by submission and report date.  
2) For each target field, use taxonomy metadata to find candidate concepts.  
3) Rank candidates by keyword and metadata signals, then choose the best available data point for each report date.  
4) Store selected values in a `RawReportDataMap` for each statement type.  
5) Build a `CompanyReport`, attach raw reports, and call `ProcessReports` to compute derived metrics.

## Technical Design

- **Mapping definition**: A configuration object containing:
  - `TargetField` (e.g., `CurTotalShareholdersEquity`).
  - `StatementType` (balance sheet, income statement, cash flow).
  - `PeriodType` (instant or duration).
  - `Keywords` and `NegativeKeywords`.
  - `PreferredConceptNames` (exact concept names to pin).
- **Candidate discovery**:
  - Query `taxonomy_concepts` by taxonomy type (initially US GAAP 2025).
  - Score concept name/label/documentation for keyword hits.
  - Exclude concepts with negative keywords or mismatched period types.
- **Selection**:
  - For each report date, select the top-ranked concept that exists in the company’s data points.
  - When multiple data points exist for the same concept/date, prefer the most recent filing or highest priority.
- **Warnings**:
  - Record when no candidate found or when multiple candidates tie.
  - Return warnings to the caller for review.
- **Multi-year taxonomies**:
  - Option A: Only 2025 taxonomy concepts, rely on EDGAR mapping to older filings.
  - Option B: Import multiple years and select the year matching the filing date.
  - Option C: Import multiple years but merge concept metadata into a unified candidate list.

## Implementation Context

- `dotnet/Stocks.DataModels/ComparisonData/CompanyReport.cs`
- `dotnet/Stocks.DataModels/ComparisonData/CashFlowItem.cs`
- `dotnet/Stocks.DataModels/ComparisonData/RawReportDataMap.cs`
- `dotnet/Stocks.Persistence/Database/Statements/GetTaxonomyConceptsStmt.cs`
- `dotnet/Stocks.Persistence/Database/DbmService.cs`
- `dotnet/Stocks.DataModels/DataPoint.cs`
- `dotnet/Stocks.Persistence/Database/Migrations/V003__UsGaapTaxonomies.sql`

## Implementation Hints

- Start with a small mapping set (cash flow + key balance sheet fields), then expand.
- Prefer exact concept-name matches over fuzzy label matches.
- Keep the mapping rules in a single configuration file to simplify iteration.
- Avoid LINQ in production code; use explicit loops for filtering/scoring.
- Use `Result` for errors and warnings collection for diagnostics.

## Open Questions

- **Name vs label**: Prioritize `name` matches; use `label`/`documentation` as secondary signals only.
- **Multi-year taxonomies**: Yes, import other years to improve coverage for older filings.
- **Year-specific mapping**: Yes, allow per-year configuration, but default to the newest year (e.g., 2025).
- **Amended filings**: Prefer amended filings over originals when multiple filings exist for the same period.
- **Auditability**: Not required.

## Glossary

- **CompanyReport**: Aggregated report with balance sheet, income statement, and cash flow data plus derived metrics.
- **CashFlowItem**: Normalized cash-flow metrics used for comparison and calculations.
- **RawReportDataMap**: Per-report map of normalized keys to numeric values.
- **Taxonomy concept**: An EDGAR taxonomy entry describing a financial concept (name, label, documentation).
- **Period type**: Whether a data point is for an instant (balance sheet) or duration (income/cash flow).
- **Mapping rule**: A configuration entry that links a target field to candidate taxonomy concepts.
