# Research Findings: Batch Scoring for Top/Bottom/All Companies Reports

## Table of Contents
1. [Scale of the Problem](#1-scale)
2. [Current Per-Company Scoring Cost](#2-query-cost)
3. [Existing Database Indexes](#3-indexes)
4. [Batch Query Feasibility](#4-batch-query)
5. [Pre-Computation Strategy](#5-pre-computation)
6. [Report Data Requirements](#6-report-data)
7. [API Shape](#7-api-shape)
8. [Price Data Bottleneck](#8-price-data)
9. [IDbmService Interface](#9-idbmservice)
10. [Existing Batch/Bulk Patterns](#10-bulk-patterns)
11. [Existing Patterns to Follow](#11-patterns)
12. [Risks and Concerns](#12-risks)
13. [Recommended Approach](#13-recommendation)

---

## 1. Scale of the Problem

The `companies` table holds all SEC EDGAR companies. The exact count requires a live DB query, but SEC EDGAR contains ~10,000+ companies with 10-K filings. `GetDashboardStatsStmt` provides aggregate counts (total companies, submissions, data points) via a CTE-based query — this confirms the system already handles large-scale aggregation.

Not all companies are "scoreable" — a company needs:
- At least 1 annual (10-K) submission with data points matching the 93 scoring concept names
- A ticker in `company_tickers` (for price lookup)
- Price data in `prices` (for market cap / P/B / estimated returns)

Companies without price data can still get partial scores (checks that don't depend on price will still evaluate), but market-cap-dependent checks (3, 6, 7, 8, 9) will be `NotAvailable`.

## 2. Current Per-Company Scoring Cost

`ScoringService.ComputeScore(companyId)` makes **4 sequential DB round-trips**:

1. **`GetScoringDataPoints`** — The heaviest query. Uses `DISTINCT ON (s.submission_id, tc.name)` with a correlated subquery that finds the 5 most recent 10-K report dates for the company. Joins `data_points` → `taxonomy_concepts` → `submissions`. Filters by `company_id`, `filing_type = 1` (10-K), and `tc.name = ANY(@concept_names)` (93 names). The inner subquery re-checks `EXISTS` on data_points to ensure the report date has relevant data. The `ORDER BY s.submission_id, tc.name, dp.end_date DESC` clause resolves duplicates by taking the latest end_date.

2. **`GetCompanyById`** — Simple PK lookup, negligible cost.

3. **`GetCompanyTickersByCompanyId`** — Indexed lookup on `company_tickers(company_id)`, negligible.

4. **`GetPricesByTicker`** — Fetches **ALL** price rows for a ticker (`SELECT * FROM prices WHERE ticker = @ticker ORDER BY price_date`), then finds max date in C#. This is wasteful — could be hundreds/thousands of rows when only the latest is needed.

**Estimated cost for all companies**: If there are ~8,000 scoreable companies, that's ~32,000 DB round-trips, with the scoring data points query being the dominant cost (complex joins, subqueries, no batch support).

## 3. Existing Database Indexes

### Relevant to scoring:
| Table | Index | Columns |
|-------|-------|---------|
| `data_points` | `idx_data_points_company_submission` | `(company_id, submission_id)` |
| `data_points` | `idx_data_points_company_submission_concept` | `(company_id, submission_id, taxonomy_concept_id)` |
| `submissions` | `idx_submissions_company_id` | `(company_id)` |
| `prices` | `idx_prices_ticker` | `(ticker)` |
| `prices` | `idx_prices_ticker_date` | `(ticker, price_date)` UNIQUE |
| `companies` | `idx_companies_cik` | `(cik)` |
| `company_tickers` | `idx_company_tickers_ticker` | `(ticker)` |
| `data_points` | `idx_data_points_upsert_key` | `(company_id, fact_name, unit_id, start_date, end_date, submission_id)` UNIQUE |

### Gaps for batch scoring:
- **No index on `taxonomy_concepts.name`** — the scoring query filters by `tc.name = ANY(@concept_names)` but there's no index on the `name` column. For a single-company query this is acceptable (the join on `taxonomy_concept_id` via `data_points` limits rows), but for a batch query across all companies this could be expensive.
- **No index on `submissions(company_id, filing_type)`** — the scoring query filters by `filing_type = 1` after the company_id filter, but there's no composite index for this.
- **No "latest price" optimization** — the current `GetPricesByTicker` fetches all rows. The `idx_prices_ticker_date` unique index supports an efficient `ORDER BY price_date DESC LIMIT 1` pattern, but the existing code doesn't use it.

## 4. Batch Query Feasibility

The scoring data points query **can** be rewritten to work across all companies, but the C# scoring logic (fallback chains, equity resolution, working capital with sign correction, deferred tax component sum) is complex and would be extremely difficult to replicate in pure SQL.

**Two viable approaches:**

### A. Batch raw data fetch, compute in C#
Rewrite `GetScoringDataPointsStmt` to drop the `@company_id` filter and return `(company_id, concept_name, value, report_date, balance_type_id)` for ALL companies. The existing C# logic in `ScoringService` (GroupByYear, ComputeDerivedMetrics, EvaluateChecks) can then process each company's data in-memory.

Sketch:
```sql
WITH ranked_dates AS (
    SELECT s2.company_id, s2.report_date,
           ROW_NUMBER() OVER (PARTITION BY s2.company_id ORDER BY s2.report_date DESC) AS rn
    FROM submissions s2
    WHERE s2.filing_type = 1
      AND EXISTS (SELECT 1 FROM data_points dp2
                  JOIN taxonomy_concepts tc2 ON dp2.taxonomy_concept_id = tc2.taxonomy_concept_id
                  WHERE dp2.submission_id = s2.submission_id AND dp2.company_id = s2.company_id
                    AND tc2.name = ANY(@concept_names))
)
SELECT sub.company_id, sub.concept_name, sub.value, sub.report_date, sub.balance_type_id
FROM (
    SELECT DISTINCT ON (dp.company_id, s.submission_id, tc.name)
        dp.company_id, tc.name AS concept_name, dp.value, s.report_date,
        tc.taxonomy_balance_type_id AS balance_type_id
    FROM data_points dp
    JOIN taxonomy_concepts tc ON dp.taxonomy_concept_id = tc.taxonomy_concept_id
    JOIN submissions s ON dp.submission_id = s.submission_id AND dp.company_id = s.company_id
    JOIN ranked_dates rd ON rd.company_id = s.company_id AND rd.report_date = s.report_date
    WHERE s.filing_type = 1
      AND tc.name = ANY(@concept_names)
      AND rd.rn <= 5
    ORDER BY dp.company_id, s.submission_id, tc.name, dp.end_date DESC
) sub
ORDER BY sub.company_id, sub.report_date DESC, sub.concept_name
```

This is a single query that returns data for all companies. It preserves the `DISTINCT ON` deduplication from the single-company version (taking the latest `end_date` per concept per submission). The result set will be large (estimated 1-2M rows for ~8K companies × 5 years × 30-50 concepts per year) but PostgreSQL can stream it via the NpgsqlDataReader.

### B. Pre-compute and store summary scores
Run approach A as a batch job (ETL step or background service), compute scores in C#, and store results in a `company_scores` summary table. The report endpoint then queries the summary table with pagination/sorting.

## 5. Pre-Computation Strategy

### Recommended: Scoring summary table (ETL approach)

**Why not materialized view?** The scoring logic involves 20+ fallback chains, sign corrections based on balance type, and multi-year averaging. This cannot be expressed in SQL without enormous complexity and maintenance burden. The C# logic in `ScoringService` is already correct and well-tested.

**Why not Redis-only?** Redis is not yet in docker-compose.yml (only PostgreSQL and pgAdmin). Adding Redis for this alone is premature. Also, the data changes infrequently (when new 10-K filings or prices are imported), not on every request.

**Why a summary table?**
- Aligns with the Statement pattern — a `BulkInsertCompanyScoresStmt` + `GetCompanyScoresStmt` follow existing conventions
- Aligns with the ETL architecture — a new CLI command (`--compute-all-scores`) fits the existing `Program.cs` orchestration
- Data changes infrequently — scores only change when new filings or prices are imported
- Report queries become simple: `SELECT * FROM company_scores ORDER BY overall_score DESC LIMIT 20 OFFSET 0`
- Can be extended with filters (exchange, min score, etc.) using standard WHERE clauses

### Summary table schema (proposed):
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
    -- Key metrics for report display / sorting / filtering
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
    -- Metadata
    computed_at timestamptz NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_company_scores_score ON company_scores (overall_score DESC);
CREATE INDEX idx_company_scores_exchange ON company_scores (exchange);
```

## 6. Report Data Requirements

Based on `ScoringResult` and `DerivedMetrics`, the report per company needs:

**Identity:** company name, CIK, ticker, exchange
**Score:** overall_score (0-13), computable_checks count, years_of_data
**Key metrics (for columns/sorting):**
- BookValue, MarketCap
- DebtToEquityRatio, PriceToBookRatio, DebtToBookRatio
- AdjustedRetainedEarnings
- AverageNetCashFlow, AverageOwnerEarnings
- EstimatedReturnCF, EstimatedReturnOE
**Price context:** PricePerShare, PriceDate, SharesOutstanding

The full per-company detail (raw data by year, all 13 check details) is already served by the existing `/api/companies/{cik}/scoring` endpoint and doesn't need to be in the batch report.

## 7. API Shape

### Existing pagination pattern (SearchCompaniesStmt):
- Input: `PaginationRequest(pageNumber, pageSize)` with `DefaultMaxPageSize = 100`
- SQL uses `LIMIT @limit OFFSET @offset` with `COUNT(*) OVER()` window function for total count
- Returns: `PagedResults<T>` with `PaginationResponse(CurrentPage, TotalItems, TotalPages)`

### Proposed report endpoint:
```
GET /api/reports/scores?page=1&pageSize=50&sortBy=overallScore&sortDir=desc&minScore=10&exchange=NYSE
```

Parameters:
- `page`, `pageSize` — standard pagination (reuse `PaginationRequest`)
- `sortBy` — column to sort by (overallScore, estimatedReturnCF, bookValue, etc.)
- `sortDir` — asc/desc
- `minScore` — filter: minimum overall score
- `maxScore` — filter: maximum overall score
- `exchange` — filter: stock exchange

Response shape: `PagedResults<CompanyScoreSummary>` where `CompanyScoreSummary` mirrors the summary table columns.

### "Top" / "Bottom" / "All" variants:
These are just different default sort orders and filters:
- **Top**: `sortBy=overallScore&sortDir=desc`
- **Bottom**: `sortBy=overallScore&sortDir=asc`
- **All**: no score filter, default sort

A single endpoint with parameters handles all three.

## 8. Price Data Bottleneck

**Current problem:** `GetPricesByTicker` fetches ALL price rows for a ticker, then C# iterates to find max date. For batch scoring, this means loading potentially millions of price rows.

**Solution:** The `idx_prices_ticker_date (ticker, price_date)` unique index already supports an efficient latest-price lookup:
```sql
SELECT close, price_date FROM prices WHERE ticker = @ticker ORDER BY price_date DESC LIMIT 1
```

For batch scoring, a single query can get the latest price for ALL tickers:
```sql
SELECT DISTINCT ON (ticker) ticker, close, price_date
FROM prices
ORDER BY ticker, price_date DESC
```

Or using the LATERAL JOIN pattern already established in `SearchCompaniesStmt`:
```sql
LEFT JOIN LATERAL (
    SELECT p.close AS latest_price, p.price_date AS latest_price_date
    FROM prices p WHERE p.ticker = ct.ticker ORDER BY p.price_date DESC LIMIT 1
) lp ON true
```

This pattern is already proven in the codebase — `SearchCompaniesStmt` uses exactly this approach.

## 9. IDbmService Interface

Full method catalog (93 lines):

**Companies:** GetCompanyById, GetAllCompaniesByDataSource, GetPagedCompaniesByDataSource, GetCompanyByCik, SearchCompanies, BulkInsertCompanies, EmptyCompaniesTables
**Company Names:** GetAllCompanyNames, GetCompanyNamesByCompanyId, BulkInsertCompanyNames
**Company Tickers:** GetCompanyTickersByCompanyId, GetAllCompanyTickers, BulkInsertCompanyTickers
**Data Points:** BulkInsertDataPoints, UpsertDataPoints, GetDataPointsForSubmission, GetScoringDataPoints, GetCompaniesWithoutSharesData
**Taxonomy:** BulkInsertTaxonomyConcepts, GetTaxonomyConceptsByTaxonomyType, BulkInsertTaxonomyPresentations, GetTaxonomyPresentationsByTaxonomyType, GetTaxonomyTypeByNameVersion, EnsureTaxonomyType, GetTaxonomyConceptCountByType, GetTaxonomyPresentationCountByType
**Submissions:** GetSubmissions, GetSubmissionsByCompanyId, BulkInsertSubmissions
**Prices:** GetPricesByTicker, BulkInsertPrices, DeletePricesForTicker, GetPriceImportStatuses, UpsertPriceImport, GetPriceDownloadStatuses, UpsertPriceDownload
**Dashboard:** GetDashboardStats
**Utilities:** DropAllTables, GetNextId64, GetIdRange64

**New methods needed for batch scoring:**
- `GetAllScoringDataPoints(string[] conceptNames, CancellationToken ct)` — batch variant of GetScoringDataPoints (no company_id filter)
- `GetLatestPrices(CancellationToken ct)` — latest price per ticker
- `TruncateCompanyScores(CancellationToken ct)` — clear summary table before rebuild
- `BulkInsertCompanyScores(List<CompanyScore> scores, CancellationToken ct)` — write summary table (uses COPY BINARY, same as other bulk inserts; truncate + insert rather than upsert, since scores are rebuilt from scratch)
- `GetCompanyScores(PaginationRequest pagination, ScoresSortBy sortBy, SortDirection sortDir, ScoresFilter? filter, CancellationToken ct)` — paginated report query

## 10. Existing Batch/Bulk Patterns

### BulkInsert pattern:
All bulk inserts use `BulkInsertDbStmtBase<T>` which leverages PostgreSQL's `COPY ... FROM STDIN (FORMAT BINARY)` for high-performance streaming writes. This is the pattern to follow for `BulkInsertCompanyScores`. Additionally, `UpsertDataPointsBatchStmt` (new) shows a `NonQueryBatchedDbStmtBase` pattern using `INSERT ... ON CONFLICT DO UPDATE` for idempotent upserts — relevant if we later want incremental score updates instead of full rebuilds.

### ETL orchestration pattern (Program.cs):
- CLI commands dispatch to async methods
- Batch size of 1000 items
- `Task.Run()` + `Task.WhenAll()` for parallel batch insertion
- Result pattern for error handling throughout

### GetDashboardStatsStmt:
Uses CTEs (Common Table Expressions) for complex aggregate queries across multiple tables. This pattern could be useful for the batch scoring data fetch query.

### SearchCompaniesStmt:
Demonstrates the pagination pattern with `COUNT(*) OVER()`, `LIMIT/OFFSET`, and LATERAL JOIN for latest price. This is the exact pattern needed for the report endpoint.

## 11. Existing Patterns to Follow

| Pattern | Source | Apply To |
|---------|--------|----------|
| Statement pattern | All `*Stmt.cs` classes | New query/insert statements |
| BulkInsert via COPY BINARY | `BulkInsertDbStmtBase<T>` | Writing scores to summary table |
| Pagination with COUNT OVER | `SearchCompaniesStmt` | Report endpoint pagination |
| LATERAL JOIN for latest price | `SearchCompaniesStmt` | Batch price lookup |
| CLI command orchestration | `Program.cs` switch | New `--compute-all-scores` command |
| Result pattern | Throughout | All new methods |
| ETL batch processing | `Program.cs` batch pattern | Processing companies in batches |
| CTE aggregates | `GetDashboardStatsStmt` | Complex batch queries |

## 12. Risks and Concerns

1. **Memory pressure**: Loading all scoring data points for all companies into memory at once could be large. Mitigation: process in batches of companies (e.g., 500 at a time) rather than all at once.

2. **Staleness**: The summary table is a snapshot. Scores become stale when new filings or prices are imported. Mitigation: re-run the batch scoring command after each ETL import. Could add a `computed_at` timestamp and warn in the UI if data is older than N days.

3. **Missing `taxonomy_concepts.name` index**: The batch scoring query filters on concept name across all companies. Adding an index on `taxonomy_concepts(name)` would improve performance significantly.

4. **Query complexity**: The batch version of `GetScoringDataPointsStmt` with ROW_NUMBER partitioned by company_id will be heavier than the single-company version. Testing with EXPLAIN ANALYZE on real data is essential.

5. **Existing ScoringService coupling**: `ComputeScore` bundles data fetching with computation. The batch approach needs to reuse the computation logic (GroupByYear, ComputeDerivedMetrics, EvaluateChecks, plus resolvers like ResolveEquity, ResolveOtherNonCash, ResolveWorkingCapitalChange) without the per-company data fetching. These internal methods are already `internal static`, so they can be called directly. Note: recent commits extracted inline fallback arrays into named chains (DebtProceedsChain, StockProceedsChain, etc.) and added a `ResolveOtherNonCash` component-sum resolver, further improving reusability.

6. **Schema migration**: Adding the `company_scores` table requires a new Evolve migration SQL file.

7. **In-memory test double**: `DbmInMemoryService` (in `dotnet/Stocks.Persistence/Database/DbmInMemoryService.cs`) implements `IDbmService` for test isolation. Any new methods added to `IDbmService` must also be implemented there, backed by `DbmInMemoryData`.

## 13. Recommended Approach

### Architecture: ETL pre-computation to summary table

1. **New migration**: Add `company_scores` table with indexes for score and exchange.

2. **New batch data fetch statement**: `GetAllScoringDataPointsStmt` — single query returning `(company_id, concept_name, value, report_date, balance_type_id)` for all companies with 10-K data, limited to 5 most recent years per company.

3. **New latest prices statement**: `GetAllLatestPricesStmt` — `DISTINCT ON (ticker)` query returning latest price per ticker.

4. **New batch scoring service method**: `ComputeAllScores()` that:
   - Fetches all scoring data points in one query
   - Fetches all latest prices in one query
   - Fetches all company tickers in one query (existing `GetAllCompanyTickers`)
   - Groups data by company_id
   - Reuses existing static methods: `GroupByYear`, `ComputeDerivedMetrics`, `EvaluateChecks`
   - Truncates then bulk inserts results into `company_scores`

5. **New CLI command**: `--compute-all-scores` in Program.cs.

6. **New report endpoint**: `GET /api/reports/scores` with pagination, sorting, filtering — backed by a simple `SELECT` on `company_scores`.

7. **New report statement**: `GetCompanyScoresStmt` with dynamic ORDER BY and WHERE clauses, pagination via LIMIT/OFFSET + COUNT OVER.

8. **Frontend**: Angular page with sortable table, score color coding, pagination, link to per-company scoring detail.

### Execution order:
1. Migration + data model
2. Batch data fetch statements
3. Batch scoring service
4. CLI command (verify correctness)
5. Report query statement + endpoint
6. Frontend

---

## Metadata
### Status
success
### Dependencies
- `ScoringService.cs` static methods (GroupByYear, ComputeDerivedMetrics, EvaluateChecks) must remain `internal static` and reusable
- Database schema must support new migration (V013 or next sequential number — V012 is taken by `AddDataPointUpsertIndex`)
- Price data and 10-K filing data must already be imported
### Open Questions
- Exact company count in the database (requires live DB query to confirm scale)
- Whether the batch scoring data fetch query performs acceptably on real data (needs EXPLAIN ANALYZE)
- Whether to also expose batch scores via gRPC (in addition to REST) — deferred for now
### Assumptions
- ~8,000-10,000 companies with 10-K filings in the database (based on SEC EDGAR universe)
- Scores change infrequently (only when new filings or prices are imported)
- The full per-company scoring detail (raw data by year, 13 checks) remains on the existing single-company endpoint; the batch report shows summary data only
- Redis is not yet available and should not be a dependency for the initial implementation
