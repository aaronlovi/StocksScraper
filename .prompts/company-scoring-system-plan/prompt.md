# Plan: Company Scoring System (13-Point Value Score)

## Context

- Research: `.prompts/company-scoring-system-research/research.md`
- Guidelines: `CLAUDE.md`
- Project fact sheet: `dotnet/project_fact_sheet.md`
- Project instructions: `dotnet/project_instructions.md`
- Reference scoring implementation (read-only): `tsx-aggregator/src/tsx-aggregator.models/AggregatorData/CompanyFullDetailReport.cs`

## Goal

Add a per-company scoring page that shows:
1. **Raw data points** — the US-GAAP concept values fetched from 10-K filings (up to 5 years), displayed in a table with one row per concept and one column per year
2. **Derived metrics** — Book Value, Adjusted Retained Earnings, Net Cash Flow, Owner Earnings, Estimated Return, etc. computed from the raw data
3. **13-point scorecard** — each check with pass/fail result, yielding a score out of 13
4. **Navigation** — linked from the company landing page

## Existing Patterns to Follow

Review CLAUDE.md for coding conventions (no LINQ, no tuples, Result pattern, etc.). Below are task-specific patterns:

- **Statement pattern** for DB queries: each operation is a class inheriting from `QueryDbStmtBase` in `dotnet/Stocks.Persistence/Database/Statements/`. See `GetDataPointsForSubmissionStmt` for a multi-row query example.
- **In-memory test doubles**: `DbmInMemoryService` implements `IDbmService`; `DbmInMemoryData` holds backing data. Every new `IDbmService` method needs both a real statement and an in-memory implementation.
- **Endpoint groups**: static classes with `Map*Endpoints` extension methods in `dotnet/Stocks.WebApi/Endpoints/`. See `CompanyEndpoints.cs` for the pattern of chaining multiple DB calls.
- **Service classes**: `StatementDataService` in `Stocks.Persistence/Services/` shows how to encapsulate complex multi-query logic. The scoring computation should follow this pattern.
- **Angular**: standalone components with signals, `api.service.ts` for HTTP calls. See `report.component.ts` for the company header reuse pattern.

## Key Design Decisions

### What to query from the database

For each company, fetch data points from 10-K filings (`filing_type = 1`) for up to 5 most recent report years. The concepts to fetch are listed in research.md section 6.1 (16 fallback chains). The query should:
- Join `data_points` → `taxonomy_concepts` (for concept name) → `submissions` (for `report_date` and `filing_type = 1`)
- Filter by `company_id` and the list of relevant concept names
- Return: concept name, value, report_date (to group by year)

Also fetch the most recent stock price from the `prices` table and shares outstanding from `CommonStockSharesOutstanding`.

### Where to compute the score

Server-side, in a new service class in `Stocks.Persistence/Services/` (following the `StatementDataService` pattern). The service receives a company ID, fetches raw data via `IDbmService`, applies fallback chains, computes derived metrics, evaluates the 13 checks, and returns a structured result. The WebApi endpoint is a thin wrapper that calls this service.

**Important**: The fallback resolution and formula computation should be pure functions (no DB dependency) so they can be unit-tested with synthetic data. Separate data fetching from data transformation.

### Response shape

The response has three tiers of detail. The exact field names and structure are for the plan to define, but the tiers are:

1. **Raw data** — concept values grouped by report year, e.g., `{ "2024": { "StockholdersEquity": 123456789 }, "2023": { ... } }`. This lets the frontend show what was fetched from the database. For each resolved field, indicate which concept was used (useful when fallbacks are involved).

2. **Derived metrics** — the intermediate computed values needed for scoring: Book Value, Market Cap, Adjusted Retained Earnings, Oldest Retained Earnings, Average Net Cash Flow, Average Owner Earnings, Estimated Return (CF and OE variants), Debt-to-Equity ratio, Price-to-Book ratio, Debt-to-Book ratio. Include `null` for metrics that couldn't be computed due to missing inputs.

3. **Scorecard** — the 13 checks, each with: check number, name, computed value (or null), threshold description, and result (`pass`, `fail`, or `na` for insufficient data). Plus overall score as `X / Y` where Y is the number of computable checks.

Also include: price per share, price date, shares outstanding, and number of years of data available.

### Frontend page design

A new route `/company/:cik/scoring` with:
- Company header (reuse pattern from report page — name, CIK, price, tickers)
- Scorecard summary (score badge like "9/13", overall pass/fail)
- Individual checks table (13 rows, showing check name, computed value, threshold, pass/fail indicator)
- Derived metrics section (key intermediate values)
- Raw data table (concepts as rows, years as columns)
- Link from company landing page (e.g., "Value Score" button/link)
- Loading and error states (following existing component patterns)

## Instructions

1. Read research.md thoroughly — especially section 1 (Formulas), section 6.1 (Concept Selection Strategy), and section 6.3 (Handling Missing Data)
2. Verify actual DB schema by reading migration files in `dotnet/Stocks.Persistence/Database/Migrations/` — confirm table/column names for `data_points`, `taxonomy_concepts`, `submissions`, and `prices` before writing SQL
3. Read existing Statement and Service examples to match patterns exactly
4. Design implementation as checkpoints following the patterns described above
5. Each checkpoint must include:
   - **Build**: what to implement (files to create/modify, key code structures)
   - **Test**: what unit tests to write for THIS checkpoint's code
   - **Verify**: how to confirm all existing + new tests pass before moving on
6. NEVER design a dedicated "testing" checkpoint at the end. Tests are written alongside the code they verify, within the same checkpoint. Each checkpoint must leave the test suite green.
7. Keep each checkpoint ≤ 2 hours of implementation work
8. Order checkpoints so each builds on the previous: data layer → scoring service → API endpoint → frontend

## Output

Write plan to `.prompts/company-scoring-system-plan/plan.md`:
- Ordered checkpoints (implementation + tests each)
- Files to create/modify per checkpoint
- Metadata block (Status, Dependencies, Open Questions, Assumptions)
