# Graham Score Backtest Requirements (Point-in-Time Snapshots + Monthly Rebalance)

## Table of Contents

- [[#1. Summary|1. Summary]]
- [[#2. Requirements Table|2. Requirements Table]]
- [[#3. Kanban Task List|3. Kanban Task List]]
- [[#4. High-Level Design|4. High-Level Design]]
- [[#5. Technical Design|5. Technical Design]]
  - [[#5.1. Technical Design - New Table|5.1. Technical Design - New Table]]
  - [[#5.2. Technical Design - As-Of Scoring|5.2. Technical Design - As-Of Scoring]]
  - [[#5.3. Technical Design - Backfill CLI|5.3. Technical Design - Backfill CLI]]
  - [[#5.4. Technical Design - REST Endpoints|5.4. Technical Design - REST Endpoints]]
  - [[#5.5. Technical Design - Backtest Computation|5.5. Technical Design - Backtest Computation]]
  - [[#5.6. Technical Design - Angular Pages|5.6. Technical Design - Angular Pages]]
- [[#6. Implementation Context|6. Implementation Context]]
- [[#7. Implementation Hints|7. Implementation Hints]]
- [[#8. Known Limitations|8. Known Limitations]]
- [[#9. Glossary|9. Glossary]]

## 1. Summary

Provide a quick back-test of the Graham 15-point scoring system. Compute the Graham score for every company **as of** past dates (month-ends going back 5 years), store the results as snapshots, and expose two new reports in the Angular portal:

1. **Point-in-time report** — pick a snapshot date, see which companies scored 15/15 then, and how each has performed from that date to today.
2. **Monthly-rebalance backtest** — simulate holding each month's 15/15 list equal-weighted for one month, rolling into the next month's list; show cumulative performance vs. SPY with month-by-month drill-down.

To avoid look-ahead bias, an as-of score may only use filings whose `acceptance_datetime` is on or before the as-of date, and the price at (or just before) the as-of date.

## 2. Requirements Table

| ID | Requirement | Description | Status | Notes |
| --- | --- | --- | --- | --- |
| 1 | Snapshot table | Create `graham_score_snapshots` keyed by `(as_of_date, company_id)` mirroring `company_scores` columns. | Proposed | Append-per-date, not truncate-replace. |
| 2 | As-of scoring data | Scoring data query accepts an as-of cutoff: only submissions with `acceptance_datetime <= as-of` are eligible. | Proposed | New statement variant of `GetAllScoringDataPointsStmt`. |
| 3 | As-of prices | Score computation at an as-of date uses the price at or just before that date. | Proposed | Reuse `GetAllPricesNearDateStmt`. |
| 4 | As-of compute | `ScoringService` supports computing all company scores as of a given date. | Proposed | Same 15 checks, different inputs. |
| 5 | Backfill CLI | CLI command computes and stores snapshots for a range of month-end dates. | Proposed | `--compute-score-snapshots --from YYYY-MM-DD --to YYYY-MM-DD`. Idempotent per date (delete + insert). |
| 6 | Snapshot dates API | Endpoint lists available snapshot dates. | Proposed | Drives the date picker. |
| 7 | Point-in-time API | Endpoint returns companies with a given min score at a snapshot date, with return from that date to today. | Proposed | Reuses returns machinery. |
| 8 | Backtest API | Endpoint returns the monthly-rebalance backtest: per-month constituents, entered/left, monthly return, cumulative value, SPY comparison. | Proposed | Equal-weighted; missing end price = held at last known price. |
| 9 | Angular point-in-time page | New route/page with snapshot-date picker and results table. | Proposed | Mirrors graham-returns UI. |
| 10 | Angular backtest page | New route/page with summary cards, cumulative chart vs SPY, expandable month rows. | Proposed | |
| 11 | Tests | Unit tests for as-of cutoff behavior, snapshot storage, and backtest chaining math. | Proposed | In-memory `IDbmService` doubles; no DB in tests. |

## 3. Kanban Task List

- [ ] Migration for `graham_score_snapshots` (with comments, indexes).
- [ ] As-of variant of the scoring data-points statement (acceptance cutoff).
- [ ] Snapshot statements: bulk insert, delete-by-date, get dates, get by date.
- [ ] Wire new statements through `IDbmService` / `DbmService` / `DbmInMemoryService`.
- [ ] `ScoringService.ComputeAllScores(asOfDate)` overload using as-of data + prices.
- [ ] CLI command `--compute-score-snapshots` with `--from` / `--to`.
- [ ] `GrahamBacktestService` (backtest chaining + SPY benchmark).
- [ ] REST endpoints: snapshot dates, point-in-time report, backtest report.
- [ ] Angular API service methods + models.
- [ ] Angular point-in-time page (`graham-snapshot` route).
- [ ] Angular backtest page (`graham-backtest` route) with chart.
- [ ] Sidebar navigation links.
- [ ] Unit tests (as-of scoring, backtest math).
- [ ] Run 5-year monthly backfill; verify reports end to end.

## 4. High-Level Design

A new snapshot table stores one row per company per as-of date, written by a new CLI backfill command. The command iterates month-end dates in a range; for each date it computes every company's Graham score using only information available on that date (filings accepted by then, price at/just before then) and stores the rows.

The point-in-time report reads one snapshot date and joins prices at that date and today (same approach as the existing graham-returns report, but sourced from the snapshot table instead of `company_scores`).

The backtest report reads all snapshot dates in order. For each consecutive pair of dates (m, m+1) it takes the companies scoring 15/15 (all computable) at m, buys them equal-weighted at the price at m, sells at the price at m+1 (or holds at last known price if missing), and chains the portfolio value. SPY over the same dates provides the benchmark.

## 5. Technical Design

### 5.1. Technical Design - New Table

`graham_score_snapshots` — same columns as `company_scores` plus `as_of_date date NOT NULL`; primary key `(as_of_date, company_id)`; index on `(as_of_date, overall_score DESC)`. Postgres `COMMENT ON` for table and columns.

### 5.2. Technical Design - As-Of Scoring

- New statement `GetAllScoringDataPointsAsOfStmt`: same SQL as `GetAllScoringDataPointsStmt` with each submissions CTE additionally filtered by `s.acceptance_datetime <= @as_of` (and `s.report_date <= @as_of` as a belt-and-braces guard). `acceptance_datetime` is 100% populated.
- `ScoringService` gains an as-of path: identical pipeline, but scoring data comes from the as-of statement and prices from `GetAllPricesNearDate(asOfDate)` instead of `GetAllLatestPrices`.
- Snapshot rows record `as_of_date`, the actual `price_date` used, and `computed_at` (run timestamp).

### 5.3. Technical Design - Backfill CLI

`--compute-score-snapshots --from 2021-07-31 --to 2026-06-30` in `Stocks.EDGARScraper`:

1. Enumerate month-end dates from `--from` to `--to` inclusive.
2. For each date: delete existing snapshot rows for that date, compute as-of scores, bulk-insert.
3. Log progress per date; continue past per-date failures (Result pattern), report a summary at the end.

### 5.4. Technical Design - REST Endpoints

- `GET /api/reports/graham-snapshot-dates` → list of `as_of_date` values present in the snapshot table.
- `GET /api/reports/graham-snapshot?asOfDate=...&minScore=15&page=&pageSize=&sortBy=&sortDir=&exchange=` → rows from the snapshot at that date, enriched with return from as-of date to today (same shape as graham-returns rows).
- `GET /api/reports/graham-backtest?minScore=15` → summary (total return, annualized, SPY total/annualized, month count) + per-month entries: as-of date, constituent count, entered/left tickers, monthly portfolio return %, cumulative value of $1000, SPY cumulative value of $1000, and the constituent detail (ticker, name, start/end price, monthly return).

### 5.5. Technical Design - Backtest Computation

New `GrahamBacktestService` in `Stocks.Persistence`:

- Load all snapshot dates; for each consecutive pair `(d[i], d[i+1])`:
  - Constituents: rows at `d[i]` with `overall_score >= minScore` and `computable_checks = 15` and a non-null ticker.
  - Start price: `GetAllPricesNearDate(d[i])`; end price: `GetAllPricesNearDate(d[i+1])` (for the last period, latest prices).
  - Per-constituent return = end close / start close − 1; company with no start price is excluded from that month; company with no end price contributes 0% for the month (held at last known price).
  - Monthly portfolio return = arithmetic mean of constituent returns (equal weight).
  - Empty 15/15 list for a month = 0% (cash) for that month.
  - Chain cumulative value multiplicatively from $1000.
- SPY benchmark: same date grid, `end/start − 1` per month, chained.

### 5.6. Technical Design - Angular Pages

- `graham-snapshot` route — copy of the graham-returns page plus a snapshot-date `<select>` (populated from the dates endpoint); table shows score, prices at snapshot and today, total/annualized return, value of $1000.
- `graham-backtest` route — summary cards (strategy total & annualized return, SPY total & annualized, months, avg constituents); cumulative-value line chart (strategy vs SPY, inline SVG — no chart library dependency); table of months, each expandable to show constituents with entered/left badges and per-stock monthly return.
- Both follow the Warm Institutional design system (`report-table.css`, CSS vars).

## 6. Implementation Context

- Scoring engine: `dotnet/Stocks.Persistence/Services/ScoringService.cs`
- Batch scoring SQL: `dotnet/Stocks.Persistence/Database/Statements/GetAllScoringDataPointsStmt.cs`
- As-of price lookup (existing): `dotnet/Stocks.Persistence/Database/Statements/GetAllPricesNearDateStmt.cs`
- Score table pattern: `dotnet/Stocks.Persistence/Database/Migrations/V013__AddCompanyScoresTable.sql`, `BulkInsertCompanyScoresStmt.cs`, `GetCompanyScoresStmt.cs`
- Returns report: `dotnet/Stocks.Persistence/Services/InvestmentReturnReportService.cs`, `dotnet/Stocks.WebApi/Endpoints/GrahamReturnsEndpoints.cs`
- CLI entry: `dotnet/Stocks.EDGARScraper/Program.cs` (`--compute-all-scores` handler as model)
- Angular reference page: `frontend/stocks-frontend/src/app/features/graham-returns-report/`
- Angular API service: `frontend/stocks-frontend/src/app/core/services/api.service.ts`

## 7. Implementation Hints

- Explicit loops, no LINQ in production code; `Result` pattern for errors; no tuples (records instead).
- Reuse `CompanyScoreSummary` for snapshot rows (add `AsOfDate` alongside, or a wrapping record) rather than duplicating the 20+ fields.
- Keep the backfill idempotent: delete-by-date before insert inside one transaction per date.
- Snapshot compute for ~60 dates re-runs the big scoring query per date; acceptable as an offline batch (~minutes per date at worst). Log per-date timing.

## 8. Known Limitations

- **Survivorship bias** — companies delisted/acquired since the as-of date may lack recent prices; the backtest holds them at last known price. EDGAR bulk data may also omit deregistered companies entirely.
- **Split skew** — Stooq prices are split-adjusted; filed share counts are not. Market-cap-derived checks (price-to-book, estimated returns) can be skewed at past dates for companies that split afterward. Month-over-month returns are unaffected.
- **Data thinning** — fewer companies have full 5-year filing histories at older as-of dates; older snapshots may contain fewer 15/15 names.

## 9. Glossary

- **As-of date** — the historical date a score is computed for, using only information public by that date.
- **Snapshot** — the stored set of all company scores computed for one as-of date.
- **Look-ahead bias** — accidentally using information (filings, prices) not yet available at the as-of date.
- **Monthly rebalance** — selling the previous month's portfolio and buying the current month's 15/15 list at each month-end.
- **Equal weight** — each constituent gets the same dollar allocation at each rebalance.
