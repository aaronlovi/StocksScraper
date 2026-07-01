# 12. Graham Score Snapshots for Backtesting

## Status
Accepted

## Context
The Graham scoring system computes 15 checks per company, but scores were only ever "as of now": the batch command truncates and replaces `company_scores`, and the scoring query always anchors on the most recent filings and latest prices. This made it impossible to answer "which companies scored 15/15 six months (or five years) ago, and how would holding them have worked out?" — a quick back-test of the scoring system.

Key questions that needed resolution:

1. How to compute a score using only information that was publicly available on a past date (avoiding look-ahead bias)?
2. Where to store historical scores, given `company_scores` is a single-snapshot table?
3. Should historical scores be computed on the fly per request, or batch-computed and stored?
4. How to simulate a monthly-rebalanced portfolio from the stored history?

## Decision

### Point-in-time correctness via acceptance-date cutoff
An as-of score may only use filings whose `submissions.acceptance_datetime` falls before the end of the as-of day (plus a `report_date <= as_of` guard). `acceptance_datetime` is 100% populated in the EDGAR bulk data and records when a filing became public — 10-Ks are accepted ~89 days after their period end on average, so filtering on `report_date` alone would leak not-yet-published fundamentals into past scores. The price leg uses the close at/just before the as-of date (existing `GetAllPricesNearDateStmt` semantics).

The cutoff is implemented as an optional parameter on the existing scoring data statement (`GetAllScoringDataPointsStmt`), and `ScoringService.ComputeAllScoresAsOf(date)` reuses the exact same 15-check pipeline as the live computation — only the two data fetches differ.

### Storage: append-per-date snapshot table
New table `graham_score_snapshots`, same columns as `company_scores` plus `as_of_date`, primary key `(as_of_date, company_id)`. Unlike `company_scores`, it accumulates history instead of truncate-replace.

### Batch backfill, not on-the-fly
A CLI command (`--compute-score-snapshots --from --to`, run.sh option 11) computes each month-end in the range (~2 minutes per date; a 5-year backfill is ~2 hours). Each date is delete-then-bulk-insert, so re-runs are idempotent and a failed month is re-runnable in isolation; per-date failures are logged and skipped rather than aborting the run. Report endpoints then read stored rows cheaply. On-the-fly computation was rejected because one as-of date costs a ~2-minute full-corpus query — unusable per page load.

### Backtest simulation
`GrahamBacktestService` chains consecutive snapshot dates: at each date, buy the companies with `overall_score >= minScore` equal-weighted; sell at the next date (the last period runs to today). A holding with no newer price is carried flat (0% for the period, matching the delisted-company reality that we cannot price the exit). A month with no qualifying companies sits in cash. SPY (already in the Stooq price history since 2005) provides the benchmark over the identical date grid. Prices are fetched per period with a ticker-filtered near-date query (`GetPricesNearDateForTickersStmt`) rather than full-table scans.

## Consequences
- The Angular portal gains two reports: `/graham-snapshot` (15/15 list at any stored date + return since) and `/graham-backtest` (monthly-rebalance simulation vs SPY with per-month constituent drill-down).
- After every data reload, `--compute-score-snapshots` must be re-run (last, after filings, tickers, prices, and inline shares) to rebuild history.
- Storage cost is negligible: ~13k rows × ~300 bytes per month-end ≈ 250 MB for 5 years.
- Known accuracy limits, acceptable for a proof of concept:
  - **Survivorship bias**: companies delisted since a snapshot date may lack exit prices (carried flat), and deregistered companies may be absent from EDGAR bulk data entirely.
  - **Split skew**: Stooq prices are split-adjusted but filed share counts are not, so market-cap-derived checks (price-to-book, estimated returns) can be skewed at past dates for companies that later split. Period returns are pure price ratios and unaffected.
  - Snapshots reflect the *current* DB contents filtered by acceptance date; amended filings accepted before the as-of date are included, which is correct point-in-time behavior, but reloading fresher EDGAR data can shift recomputed history slightly.

## Alternatives Considered
- **Filter on `report_date` only**: Rejected — a 10-K's fundamentals are not public on the period end date; using them ~3 months early is classic look-ahead bias.
- **Filter on `data_points.filed_date`**: Viable (also 100% populated) but per-row filtering complicates the query; the submission-level `acceptance_datetime` is the natural grain since availability is a property of the filing, not the data point.
- **History via `computed_at` in `company_scores`**: Keep the existing table and stop truncating. Rejected — the PK is `company_id` alone, live "scores now" reads would need max-timestamp filtering everywhere, and live vs historical rows have different semantics (latest prices vs as-of prices).
- **Compute snapshots on demand per report request**: Rejected on cost (~2 minutes per date per request).
- **Store only the 15/15 winners per date**: Rejected — storing all companies (~13k/month) costs little and lets the min-score threshold be varied after the fact (e.g., backtest 14+ without recomputing).

---
